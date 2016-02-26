namespace Vault.Core.Data
{
    public interface IStructureService
    {
        VaultStream OpenFile(string file);

        VaultStream CreateFile(string file);

        void Delete(string path);

        FileEntity GetFileInfo(string path);

        DirectoryEntity GetDirectoryInfo(string path);
    }
}