namespace Vault.Core.Structure
{
    public interface IStructureService
    {
        EntityInfo GetEntityInfo(string path);

        EntityInfo CraeteFile(string path);

        EntityInfo CreateDirectory(string path);
    }

    public class StructureService : IStructureService
    {


        public EntityInfo GetEntityInfo(string path)
        {
            throw new System.NotImplementedException();
        }

        public EntityInfo CraeteFile(string path)
        {
            throw new System.NotImplementedException();
        }

        public EntityInfo CreateDirectory(string path)
        {
            throw new System.NotImplementedException();
        }
    }
}
