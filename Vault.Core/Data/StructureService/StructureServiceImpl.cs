using System.Collections.Generic;
using System.IO;

namespace Vault.Core.Data
{
    public class StructureServiceImpl : IStructureService
    {
        public StructureServiceImpl(Stream stream)
        {
            _stream = stream;
            var service = new RecordService(_stream);
        }

        public VaultStream OpenFile(string file)
        {
            throw new System.NotImplementedException();
        }

        public VaultStream CreateFile(string file)
        {
            throw new System.NotImplementedException();
        }

        public void Delete(string path)
        {
            throw new System.NotImplementedException();
        }

        public FileEntity GetFileInfo(string path)
        {
            throw new System.NotImplementedException();
        }

        public DirectoryEntity GetDirectoryInfo(string path)
        {
            throw new System.NotImplementedException();
        }

        

        private readonly Stream _stream;
        private readonly Dictionary<string, Record> _cache = new Dictionary<string, Record>(); 
    }
}
