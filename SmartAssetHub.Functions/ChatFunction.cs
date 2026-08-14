using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text;
using System.Text.Json;

namespace SmartAssetHub.Functions;

public class ChatFunction
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly string _tableName;

    public ChatFunction()
    {
        _dynamoDbClient = new AmazonDynamoDBClient();
        _bedrockClient = new AmazonBedrockRuntimeClient();
        _tableName = Environment.GetEnvironmentVariable("TABLE_NAME") ?? "SmartAssetMetadata";
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var headers = new Dictionary<string, string>
        {
            { "Content-Type", "application/json" },
            { "Access-Control-Allow-Origin", "*" },
            { "Access-Control-Allow-Headers", "Content-Type" },
            { "Access-Control-Allow-Methods", "OPTIONS,POST,GET" }
        };

        try
        {
            var body = JsonSerializer.Deserialize<ChatRequest>(request.Body ?? "{}");
            if (string.IsNullOrWhiteSpace(body?.Question))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = JsonSerializer.Serialize(new { error = "A pergunta é obrigatória." }),
                    Headers = headers
                };
            }

            //Implementação da logica do RAG
            var scanResult = await _dynamoDbClient.ScanAsync(new ScanRequest { TableName = _tableName });
            var contextText = new StringBuilder();

            foreach (var item in scanResult.Items)
            {
                var fileName = item.ContainsKey("FileName") ? item["FileName"].S : "Desconhecido";
                var extractedText = item.ContainsKey("ExtractedText") ? item["ExtractedText"].S : "";
                var tags = item.ContainsKey("Tags") ? string.Join(", ", item["Tags"].SS) : "";

                contextText.AppendLine($"--- Arquivo: {fileName} ---");
                if (!string.IsNullOrEmpty(tags)) contextText.AppendLine($"Tags: {tags}");
                if (!string.IsNullOrEmpty(extractedText)) contextText.AppendLine($"Texto: {extractedText}");
                contextText.AppendLine();
            }

            // prompt personalizado para o FM Claude utilizando o Bedrock
            var prompt = $@"Você é um assistente inteligente especialista em análise de documentos.
            Abaixo está o conteúdo extraído de todos os arquivos do sistema:

            [INÍCIO DO CONTEXTO]
            {contextText}
            [FIM DO CONTEXTO]

            Com base estritamente no contexto acima, responda à seguinte pergunta do usuário:
            Pergunta: {body.Question}

            Se a informação não estiver nos arquivos, diga educadamente que não encontrou essa informação nos documentos cadastrados.";

            var bedrockPayload = new
            {
                anthropic_version = "bedrock-2023-05-31",
                max_tokens = 500,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var bedrockResponse = await _bedrockClient.InvokeModelAsync(new InvokeModelRequest
            {
                ModelId = "anthropic.claude-3-haiku-20240307-v1:0",
                ContentType = "application/json",
                Accept = "application/json",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(bedrockPayload)))
            });

            using var reader = new StreamReader(bedrockResponse.Body);
            var responseBody = await reader.ReadToEndAsync();
            using var doc = JsonDocument.Parse(responseBody);

            var aiAnswer = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(new { answer = aiAnswer }),
                Headers = headers
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Erro ao processar chat: {ex.Message}");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = JsonSerializer.Serialize(new { error = "Erro interno ao processar resposta.", details = ex.Message }),
                Headers = headers
            };
        }
    }
}

public class ChatRequest
{
    public string Question { get; set; } = string.Empty;
}