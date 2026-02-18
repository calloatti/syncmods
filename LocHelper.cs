using Timberborn.Localization;
using Timberborn.EntitySystem;
using Bindito.Core;

namespace Calloatti.SyncMods
{
    public static class LocHelper
    {
        public static ILoc Loc { get; internal set; }

        public static string T(string key)
        {
            if (Loc == null)
            {
                Log.Info($"LocHelper: Advertencia - Se intento traducir '{key}' antes de inicializar el servicio.");
                return key;
            }

            string translation = Loc.T(key);

            if (string.IsNullOrEmpty(translation) || translation == key)
            {
                Log.Info($"LocHelper: No se encontro traduccion para la llave '{key}' en los archivos .csv.");
            }

            return translation;
        }

        public static string T(string key, params object[] args)
        {
            if (Loc == null)
            {
                Log.Info($"LocHelper: Advertencia - Se intento traducir '{key}' con parametros antes de inicializar.");
                return key;
            }

            return Loc.T(key, args);
        }

        [Context("MainMenu")]
        [Context("Game")]
        private class LocProxyInitializer : IInitializableEntity
        {
            public LocProxyInitializer(ILoc loc)
            {
                LocHelper.Loc = loc;
                Log.Info("LocHelper: Servicio ILoc inyectado correctamente.");
            }

            public void InitializeEntity() { }
        }

        public static void Register(IContainerDefinition containerDefinition)
        {
            containerDefinition.Bind<LocProxyInitializer>().AsSingleton();
        }
    }
}