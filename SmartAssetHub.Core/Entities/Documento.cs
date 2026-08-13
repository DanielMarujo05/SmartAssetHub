using Amazon.DynamoDBv2.DataModel;

namespace SmartAssetHub.Core.Entities;

[DynamoDBTable("SmartAssetMetadata")]
public class Documento
{
    [DynamoDBHashKey]
    public string DocumentId { get; set; } = Guid.NewGuid().ToString();

    [DynamoDBProperty]
    public string NomeArquivo { get; set; } = string.Empty;

    [DynamoDBProperty]
    public string ChaveS3 { get; set; } = string.Empty;

    [DynamoDBProperty]
    public string? ResumoIA { get; set; }

    [DynamoDBProperty]
    public string StatusProcessamento { get; set; } = "PENDENTE";

    [DynamoDBProperty]
    public DateTime DataUpload { get; set; } = DateTime.UtcNow;
}