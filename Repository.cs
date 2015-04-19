using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuSpecHelper
{
    public class Repository
    {
        private static Repository instance;

        private Dictionary<string, PackageIdentity> _repos = new Dictionary<string, PackageIdentity>();

        private Repository()
        {
            
        }

        void Add(PackageIdentity addPackagey)
        {
            _repos.Add(addPackagey.Id, addPackagey);
        }

        public static Repository Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Repository();
                }
                return instance;
            }
        }

        internal static IEnumerable<PackageIdentity> GetAllDependecies(string Id)
        {
            return Instance.getAllDependecies(Id);
        }
        private IEnumerable<PackageIdentity> getAllDependecies(string Id)
        {
            if (!_repos.ContainsKey(Id))
                yield break;
            // _repos[Id]
 	        
        }
    }
}
