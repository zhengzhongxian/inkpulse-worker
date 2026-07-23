namespace InkPulse.Worker.Infrastructure.Services.Security.Interfaces
{
    public interface ICryptographyService
    {
        string EncryptAes(string plainText);
        string DecryptAes(string cipherText);
    }
}
