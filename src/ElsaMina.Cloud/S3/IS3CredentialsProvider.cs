namespace ElsaMina.Cloud.S3;

public interface IS3CredentialsProvider
{
    string S3BucketName { get; }
    string S3EndpointUrl { get; }
    string S3AccessKey { get; }
    string S3SecretKey { get; }
    string S3BaseUrl { get; }
}