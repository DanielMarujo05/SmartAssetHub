using Amazon.CDK;

namespace SmartAssetHub.Infra;

internal class Program
{
    private static void Main(string[] args)
    {
        var app = new App();

        new SmartAssetStack(app, "SmartAssetHubStack", new StackProps
        {
            Env = new Amazon.CDK.Environment
            {
                Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION") ?? "us-east-1"
            }
        });

        app.Synth();
    }
}