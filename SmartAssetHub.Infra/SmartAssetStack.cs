using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Notifications;
using Constructs;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace SmartAssetHub.Infra;

public class SmartAssetStack : Stack
{
    public SmartAssetStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {

        var bucket = new Bucket(this, "SmartAssetUploadBucket", new BucketProps
        {
            Versioned = true,
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true,
            Cors = new[]
            {
                new CorsRule
                {
                    AllowedMethods = new[] { HttpMethods.PUT, HttpMethods.POST, HttpMethods.GET },
                    AllowedOrigins = new[] { "*" },
                    AllowedHeaders = new[] { "*" }
                }
            }
        });


        var table = new Table(this, "SmartAssetMetadataTable", new TableProps
        {
            TableName = "SmartAssetMetadata",
            PartitionKey = new Attribute { Name = "DocumentId", Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        var publishPath = "../SmartAssetHub.Functions/bin/Release/net8.0/linux-x64/publish";


        var processorFunction = new Function(this, "SmartAssetProcessorFunction", new FunctionProps
        {
            Runtime = Runtime.DOTNET_8,
            Handler = "SmartAssetHub.Functions::SmartAssetHub.Functions.Function::FunctionHandler",
            Code = Code.FromAsset(publishPath),
            Timeout = Duration.Seconds(600),
            MemorySize = 512,
            Environment = new System.Collections.Generic.Dictionary<string, string>
            {
                { "TABLE_NAME", table.TableName }
            }
        });

        table.GrantReadWriteData(processorFunction);
        bucket.GrantRead(processorFunction);

        processorFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[]
            {
                "rekognition:DetectLabels",
                "rekognition:DetectText",
                "textract:StartDocumentTextDetection",
                "textract:GetDocumentTextDetection",
                "textract:DetectDocumentText",
                "textract:AnalyzeDocument",
                "comprehend:DetectKeyPhrases",
                "comprehend:DetectEntities",
                "comprehend:DetectDominantLanguage"
            },
            Resources = new[] { "*" }
        }));

        bucket.AddEventNotification(EventType.OBJECT_CREATED, new LambdaDestination(processorFunction));


        var chatFunction = new Function(this, "SmartAssetChatFunction", new FunctionProps
        {
            Runtime = Runtime.DOTNET_8,
            Handler = "SmartAssetHub.Functions::SmartAssetHub.Functions.ChatFunction::FunctionHandler",
            Code = Code.FromAsset(publishPath),
            Timeout = Duration.Seconds(30),
            MemorySize = 512,
            Environment = new System.Collections.Generic.Dictionary<string, string>
            {
                { "TABLE_NAME", table.TableName }
            }
        });

        table.GrantReadData(chatFunction);
        chatFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[] { "bedrock:InvokeModel" },
            Resources = new[] { "*" }
        }));


        var uploadUrlFunction = new Function(this, "SmartAssetUploadUrlFunction", new FunctionProps
        {
            Runtime = Runtime.DOTNET_8,
            Handler = "SmartAssetHub.Functions::SmartAssetHub.Functions.ApiFunction::HandleGetUploadUrl",
            Code = Code.FromAsset(publishPath),
            Timeout = Duration.Seconds(15),
            MemorySize = 256,
            Environment = new System.Collections.Generic.Dictionary<string, string>
            {
                { "BUCKET_NAME", bucket.BucketName },
                { "TABLE_NAME", table.TableName }
            }
        });

        bucket.GrantReadWrite(uploadUrlFunction);
        table.GrantReadData(uploadUrlFunction);

        var assetsFunction = new Function(this, "SmartAssetAssetsFunction", new FunctionProps
        {
            Runtime = Runtime.DOTNET_8,
            Handler = "SmartAssetHub.Functions::SmartAssetHub.Functions.ApiFunction::HandleGetAssets",
            Code = Code.FromAsset(publishPath),
            Timeout = Duration.Seconds(15),
            MemorySize = 256,
            Environment = new System.Collections.Generic.Dictionary<string, string>
            {
                { "BUCKET_NAME", bucket.BucketName },
                { "TABLE_NAME", table.TableName }
            }
        });

        table.GrantReadData(assetsFunction);


        var api = new RestApi(this, "SmartAssetApi", new RestApiProps
        {
            RestApiName = "SmartAssetHub API",
            DefaultCorsPreflightOptions = new CorsOptions
            {
                AllowOrigins = Cors.ALL_ORIGINS,
                AllowMethods = Cors.ALL_METHODS
            }
        });

        var uploadUrlResource = api.Root.AddResource("upload-url");
        uploadUrlResource.AddMethod("GET", new LambdaIntegration(uploadUrlFunction));

        var assetsResource = api.Root.AddResource("assets");
        assetsResource.AddMethod("GET", new LambdaIntegration(assetsFunction));

        var chatResource = api.Root.AddResource("chat");
        chatResource.AddMethod("POST", new LambdaIntegration(chatFunction));

        new CfnOutput(this, "ApiGatewayUrl", new CfnOutputProps { Value = api.Url });
    }
}