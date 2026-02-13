using UnityEngine;

namespace ProjectA.Core
{
    public static class ProjectAConfigLoader
    {
        private const string ResourcesPath = "ProjectA/ProjectAConfig";
        private static ProjectAConfig _cached;

        public static ProjectAConfig Load()
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<ProjectAConfig>(ResourcesPath);
            if (_cached == null)
            {
                ProjectALogger.Error($"ProjectA config missing at Resources/{ResourcesPath}.asset");
            }

            return _cached;
        }
    }
}
