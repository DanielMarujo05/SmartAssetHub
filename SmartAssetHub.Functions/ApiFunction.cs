using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text.Json;

namespace SmartAssetHub.Functions;

public class ApiFunction
{
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly string _bucketName;
    private readonly string _tableName;

    public ApiFunction()
    {
        _s3Client = new AmazonS3Client();
        _dynamoDbClient = new AmazonDynamoDBClient();
        _bucketName = Environment.GetEnvironmentVariable("BUCKET_NAME") ?? "";
        _tableName = Environment.GetEnvironmentVariable("TABLE_NAME") ?? "SmartAssetMetadata";
    }

    public async Task<APIGatewayProxyResponse> HandleGetUploadUrl(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var headers = GetCorsHeaders();
        var fileName = request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey("fileName")
            ? request.QueryStringParameters["fileName"]
            : $"{Guid.NewGuid()}.bin";

        var urlRequest = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = fileName,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(10)
        };

        string presignedUrl = _s3Client.GetPreSignedURL(urlRequest);

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(new { uploadUrl = presignedUrl, key = fileName }),
            Headers = headers
        };
    }

    public async Task<APIGatewayProxyResponse> HandleGetAssets(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var headers = GetCorsHeaders();
        var scan = await _dynamoDbClient.ScanAsync(new ScanRequest { TableName = _tableName });

        var items = scan.Items.Select(item => new
        {
            DocumentId = item.ContainsKey("DocumentId") ? item["DocumentId"].S : "",
            FileName = item.ContainsKey("FileName") ? item["FileName"].S : "",
            FileType = item.ContainsKey("FileType") ? item["FileType"].S : "",
            ProcessedAt = item.ContainsKey("ProcessedAt") ? item["ProcessedAt"].S : "",
            Tags = item.ContainsKey("Tags") ? item["Tags"].SS : new List<string>(),
            ExtractedText = item.ContainsKey("ExtractedText") ? item["ExtractedText"].S : ""
        });

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(items),
            Headers = headers
        };
    }

    private Dictionary<string, string> GetCorsHeaders() => new()
    {
        { "Content-Type", "application/json" },
        { "Access-Control-Allow-Origin", "*" },
        { "Access-Control-Allow-Headers", "Content-Type" },
        { "Access-Control-Allow-Methods", "OPTIONS,POST,GET" }
    };
}