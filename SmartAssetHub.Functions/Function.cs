using Amazon.Comprehend;
using Amazon.Comprehend.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.Textract;
using Amazon.Textract.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SmartAssetHub.Functions;

public class Function
{
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IAmazonRekognition _rekognitionClient;
    private readonly IAmazonTextract _textractClient;
    private readonly IAmazonComprehend _comprehendClient;
    private readonly string _tableName;

    public Function()
    {
        _s3Client = new AmazonS3Client();
        _dynamoDbClient = new AmazonDynamoDBClient();
        _rekognitionClient = new AmazonRekognitionClient();
        _textractClient = new AmazonTextractClient();
        _comprehendClient = new AmazonComprehendClient();
        _tableName = Environment.GetEnvironmentVariable("TABLE_NAME") ?? "SmartAssetMetadata";
    }

    public async Task FunctionHandler(S3Event s3Event, ILambdaContext context)
    {
        foreach (var record in s3Event.Records)
        {
            var bucketName = record.S3.Bucket.Name;

            var rawKey = record.S3.Object.Key.Replace("+", " ");
            var objectKey = Uri.UnescapeDataString(rawKey);

            var extension = Path.GetExtension(objectKey).ToLower();

            context.Logger.LogInformation($"[PROCESSANDO] Arquivo: {objectKey} no Bucket: {bucketName}");

            string extractedText = "";
            var tags = new List<string>();

            try
            {
                if (extension == ".jpg" || extension == ".png" || extension == ".jpeg")
                {
                    context.Logger.LogInformation("Executando AWS Rekognition (Imagem)...");
                    tags = await ProcessImageWithRekognitionAsync(bucketName, objectKey);
                }
                else if (extension == ".pdf")
                {
                    context.Logger.LogInformation("Executando AWS Textract (Documento PDF)...");
                    extractedText = await ProcessPdfWithTextractAsync(bucketName, objectKey, context);
                }

                var keyEntities = new List<string>();
                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    context.Logger.LogInformation("Executando AWS Comprehend no texto extraído...");
                    keyEntities = await ExtractEntitiesWithComprehendAsync(extractedText);
                }

                await SaveToDynamoDbAsync(objectKey, extension, tags, extractedText, keyEntities);
                context.Logger.LogInformation($"[SUCESSO] Processamento de {objectKey} concluído!");
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"[ERRO CRÍTICO] Falha ao processar {objectKey}: {ex.Message}");
                context.Logger.LogError(ex.StackTrace);
                throw; // Lança a exceção para registrar no CloudWatch
            }
        }
    }

    private async Task<List<string>> ProcessImageWithRekognitionAsync(string bucket, string key)
    {
        var request = new DetectLabelsRequest
        {
            Image = new Amazon.Rekognition.Model.Image
            {
                S3Object = new Amazon.Rekognition.Model.S3Object { Bucket = bucket, Name = key }
            },
            MaxLabels = 10,
            MinConfidence = 75F
        };

        var response = await _rekognitionClient.DetectLabelsAsync(request);
        return response.Labels.Select(l => l.Name).ToList();
    }

    private async Task<string> ProcessPdfWithTextractAsync(string bucket, string key, ILambdaContext context)
    {

        var startRequest = new StartDocumentTextDetectionRequest
        {
            DocumentLocation = new DocumentLocation
            {
                S3Object = new Amazon.Textract.Model.S3Object
                {
                    Bucket = bucket,
                    Name = key
                }
            }
        };

        var startResponse = await _textractClient.StartDocumentTextDetectionAsync(startRequest);
        string jobId = startResponse.JobId;

        GetDocumentTextDetectionResponse getResponse;
        do
        {
            await Task.Delay(3000);

            getResponse = await _textractClient.GetDocumentTextDetectionAsync(new GetDocumentTextDetectionRequest
            {
                JobId = jobId
            });

        } while (getResponse.JobStatus == Amazon.Textract.JobStatus.IN_PROGRESS);

        if (getResponse.JobStatus != Amazon.Textract.JobStatus.SUCCEEDED)
        {
            throw new Exception($"Textract falhou com status: {getResponse.JobStatus}");
        }

        var lines = new List<string>();
        string? nextToken = null;

        do
        {
            var pageResponse = await _textractClient.GetDocumentTextDetectionAsync(new GetDocumentTextDetectionRequest
            {
                JobId = jobId,
                NextToken = nextToken
            });

            var pageLines = pageResponse.Blocks
                .Where(b => b.BlockType == Amazon.Textract.BlockType.LINE)
                .Select(b => b.Text);

            lines.AddRange(pageLines);
            nextToken = pageResponse.NextToken;

        } while (!string.IsNullOrEmpty(nextToken));

        return string.Join(" ", lines);
    }

    private async Task<List<string>> ExtractEntitiesWithComprehendAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var textToAnalyze = text.Length > 4500 ? text.Substring(0, 4500) : text;

        var languageRequest = new DetectDominantLanguageRequest
        {
            Text = textToAnalyze
        };

        var languageResponse = await _comprehendClient.DetectDominantLanguageAsync(languageRequest);
        var topLanguage = languageResponse.Languages.OrderByDescending(l => l.Score).FirstOrDefault();

        string detectedCode = (topLanguage != null && topLanguage.Score > 0.5)
            ? topLanguage.LanguageCode
            : "en";

        LanguageCode languageCode = LanguageCode.FindValue(detectedCode) ?? LanguageCode.En;

        var request = new DetectEntitiesRequest
        {
            Text = textToAnalyze,
            LanguageCode = languageCode
        };

        var response = await _comprehendClient.DetectEntitiesAsync(request);

        return response.Entities
            .Where(e => e.Score > 0.7)
            .Select(e => $"{e.Type}: {e.Text}")
            .Distinct()
            .ToList();
    }

    private async Task SaveToDynamoDbAsync(string key, string extension, List<string> tags, string extractedText, List<string> entities)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["DocumentId"] = new AttributeValue { S = Guid.NewGuid().ToString() },
            ["FileName"] = new AttributeValue { S = key },
            ["FileType"] = new AttributeValue { S = extension },
            ["ProcessedAt"] = new AttributeValue { S = DateTime.UtcNow.ToString("o") },
            ["ExtractedText"] = new AttributeValue { S = string.IsNullOrWhiteSpace(extractedText) ? "N/A" : extractedText }
        };

        if (tags.Any())
            item["Tags"] = new AttributeValue { SS = tags };

        if (entities.Any())
            item["Entities"] = new AttributeValue { SS = entities };

        await _dynamoDbClient.PutItemAsync(_tableName, item);
    }
}