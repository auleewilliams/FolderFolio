namespace FolderFolio.Imaging;

public sealed class StaleSourceException : InvalidOperationException
{
    public StaleSourceException()
        : base("The indexed photo source no longer matches its fingerprint.")
    {
    }
}
