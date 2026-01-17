using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using OfflineProjectManager.Services.Interop;

namespace OfflineProjectManager.Services
{
    public class PreviewHandlerService
    {
        // Find the CLSID of the Preview Handler for a specific file extension (e.g., .dwg)
        public static Guid GetPreviewHandlerGUID(string filename)
        {
            string ext = System.IO.Path.GetExtension(filename);
            if (string.IsNullOrEmpty(ext)) return Guid.Empty;

            Guid handlerGuid;

            // 1. Search in HKCR\.ext\shellex\{...}
            handlerGuid = GetGuidFromShellEx(Registry.ClassesRoot, ext);
            if (handlerGuid != Guid.Empty) return handlerGuid;

            // 2. Search in HKCR\ProgID\shellex\{...}
            using (RegistryKey extKey = Registry.ClassesRoot.OpenSubKey(ext))
            {
                if (extKey != null)
                {
                    string progId = extKey.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(progId))
                    {
                        handlerGuid = GetGuidFromShellEx(Registry.ClassesRoot, progId);
                        if (handlerGuid != Guid.Empty) return handlerGuid;
                    }
                }
            }

            // 3. Search in HKCR\SystemFileAssociations\.ext\shellex\{...}
            handlerGuid = GetGuidFromShellEx(Registry.ClassesRoot, "SystemFileAssociations\\" + ext);
            if (handlerGuid != Guid.Empty) return handlerGuid;

            // Fallback: If it's a DWG and we still don't have it, try common AutoCAD/TrueView CLSIDs
            if (ext.Equals(".dwg", StringComparison.OrdinalIgnoreCase))
            {
                // Try common DWG Preview Handler CLSIDs
                string[] commonDwgHandlers =
                [
                    "{AC1DB655-4F9A-4C39-8AD2-A65324A4C446}", // AutoCAD/TrueView
                    "{92BFBA04-597E-470F-B3FA-9E86E50836C6}", // DWG TrueView
                    "{8A049961-05A9-4972-88B1-B1A1E7582967}"  // Another version
                ];

                foreach (var clsid in commonDwgHandlers)
                {
                    if (IsHandlerRegistered(clsid))
                    {
                        return new Guid(clsid);
                    }
                }
            }

            return Guid.Empty;
        }

        private static Guid GetGuidFromShellEx(RegistryKey rootKey, string path)
        {
            try
            {
                using RegistryKey shellExKey = rootKey.OpenSubKey(path + "\\shellex\\{8895b1c6-b41f-4c1c-a562-0d564250836f}");
                if (shellExKey != null)
                {
                    string guid = shellExKey.GetValue(null) as string;
                    if (Guid.TryParse(guid, out Guid result)) return result;
                }
            }
            catch { }
            return Guid.Empty;
        }

        private static bool IsHandlerRegistered(string clsid)
        {
            try
            {
                using RegistryKey key = Registry.ClassesRoot.OpenSubKey("CLSID\\" + clsid);
                return key != null;
            }
            catch { return false; }
        }

        // Initialize a COM object from a GUID
        public static object CreatePreviewHandler(Guid clsid)
        {
            try
            {
                Type comType = Type.GetTypeFromCLSID(clsid);
                if (comType == null) return null;
                return Activator.CreateInstance(comType);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating preview handler for {clsid}: {ex.Message}");
                return null;
            }
        }
    }
}
