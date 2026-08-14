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

//assembly para habilitar a conversao da entrada JSON da funcao Lambda em uma classe .NET
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
            var objectKey = System.Net.WebUtility.UrlDecode(record.S3.Object.Key);
            var extension = Path.GetExtension(objectKey).ToLower();

            context.Logger.LogInformation($"[PROCESSANDO] Arquivo: {objectKey} no Bucket: {bucketName}");

            string extractedText = "";
            var tags = new List<string>();

            //processa o tipo e manda pra seu respectivo serviço AWS para processamento e geração de metadados
            if (extension == ".jpg" || extension == ".png" || extension == ".jpeg")
            {
                context.Logger.LogInformation("Executando AWS Rekognition (Imagem)...");
                tags = await ProcessImageWithRekognitionAsync(bucketName, objectKey);
            }
            else if (extension == ".pdf")
            {
                context.Logger.LogInformation("Executando AWS Textract (Documento PDF)...");
                extractedText = await ProcessPdfWithTextractAsync(bucketName, objectKey);
            }

            //analisa o texto extraido com AWS Comprehend para extrair entidades-chave e sentimentos
            var keyEntities = new List<string>();
            if (!string.IsNullOrWhiteSpace(extractedText))
            {
                context.Logger.LogInformation("Executando AWS Comprehend no texto extraído...");
                keyEntities = await ExtractEntitiesWithComprehendAsync(extractedText);
            }

            //salva os metadados no DynamoDB
            await SaveToDynamoDbAsync(objectKey, extension, tags, extractedText, keyEntities);
            context.Logger.LogInformation($"[SUCESSO] Processamento de {objectKey} concluído!");
        }
    }

    private async Task<List<string>> ProcessImageWithRekognitionAsync(string bucket, string key)
    {
        var request = new DetectLabelsRequest
        {
            Image = new Image
            {
                S3Object = new Amazon.Rekognition.Model.S3Object { Bucket = bucket, Name = key }
            },
            MaxLabels = 10,
            MinConfidence = 75F
        };

        var response = await _rekognitionClient.DetectLabelsAsync(request);
        return response.Labels.Select(l => l.Name).ToList();
    }

    private async Task<string> ProcessPdfWithTextractAsync(string bucket, string key)
    {
        var request = new DetectDocumentTextRequest
        {
            Document = new Amazon.Textract.Model.Document
            {
                S3Object = new Amazon.Textract.Model.S3Object { Bucket = bucket, Name = key }
            }
        };

        var response = await _textractClient.DetectDocumentTextAsync(request);
        var lines = response.Blocks
            .Where(b => b.BlockType == Amazon.Textract.BlockType.LINE)
            .Select(b => b.Text);

        return string.Join(" ", lines);
    }

    private async Task<List<string>> ExtractEntitiesWithComprehendAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var textToAnalyze = text.Length > 4500 ? text.Substring(0, 4500) : text;

        //pega o idioma dominante do texto para passar para a função de extração de entidades, caso o idioma não seja identificado, o fallback será inglês ('en')
        var languageRequest = new DetectDominantLanguageRequest
        {
            Text = textToAnalyze
        };

        var languageResponse = await _comprehendClient.DetectDominantLanguageAsync(languageRequest);


        var topLanguage = languageResponse.Languages
            .OrderByDescending(l => l.Score)
            .FirstOrDefault();


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