namespace Vault.Core.Data.StructureService
{
    internal class PathParser
    {
        public string[] SplitPathToRecords(string path)
        {
            return path.Split('\\');
        }
    }
}
