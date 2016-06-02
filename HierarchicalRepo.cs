using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet;

namespace NuSpecHelper
{
    public class HierarchicalRepo
    {
        public IPackageRepository Repository;

        private readonly IPackageRepository[] _repos = new IPackageRepository[3];

        internal IPackageRepository GetRepo(string branch = "nuget")
        {
            var repoId = GetIndex(branch);
            if (_repos[repoId] != null)
            {
                return _repos[repoId];
            }
            var repo = PackageRepositoryFactory.Default.CreateRepository(GetUrl(repoId));
            _repos[repoId] = repo;
            return repo;
        }

       

        private static int GetIndex(string branch)
        {
            switch (branch)
            {
                case "nuget":
                    return 0;
                case "master":
                    return 1;
            }
            return 2;
        }

        private static string GetUrl(int level)
        {
            switch (level)
            {
                case 0:
                    return "https://packages.nuget.org/api/v2";
                case 1:
                    return "https://www.myget.org/F/xbim-master/api/v2";
            }
            return "https://www.myget.org/F/xbim-develop/api/v2";
        }

    }
}
