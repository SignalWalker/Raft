using System;

namespace Raft {
    using System.Runtime.InteropServices;
    using System.Text;
    using SDL2;
    using VulkanCore;
    using VulkanCore.Khr;

    public class Forge {
        const int EngineVersion = 1;

        string name;
        int version;

        public void Init(Window window) {
            ApplicationInfo appInfo = new ApplicationInfo {
                ApplicationName = name,
                ApplicationVersion = version,
                EngineName = "Raft",
                EngineVersion = EngineVersion,
                ApiVersion = new Version(1, 0, 61)
            };

            IntPtr[] pNames = new IntPtr[0];
            // you have to call this twice because something, somewhere, is terrible
            SDL.SDL_Vulkan_GetInstanceExtensions(window.handle, out uint pCount, null);
            pNames = new IntPtr[pCount];
            SDL.SDL_Vulkan_GetInstanceExtensions(window.handle, out pCount, pNames);

            InstanceCreateInfo instInfo = new InstanceCreateInfo() {
                ApplicationInfo = appInfo,
                EnabledExtensionNames = pNamesToStrings(pNames)
            };

            Instance inst = new Instance(instInfo);

            // make surface

            SurfaceKhr surf = window.GetSurface(inst);
        }

        string[] pNamesToStrings(IntPtr[] pNames) {
            string[] res = new string[pNames.Length];
            for (int i = 0; i < pNames.Length; i++) {
                IntPtr ptr = pNames[i];
                //res[i] = Marshal.PtrToStringUni(ptr);
                res[i] = PtrToString(ptr);
                Console.Out.WriteLine(res[i]);
            }

            return res;
        }

        string PtrToString(IntPtr ptr) {
            // the character array from the C-struct is of length 32
            // char types are 8-bit in C, but 16-bit in C#, so we use a byte (8-bit) here
            byte[] rawBytes = new byte[32];
            //using 32 because everything is garbage

            // we have a pointer to an unmanaged character array from the SDL2 lib (event.text.text),
            // so we need to explicitly marshal into our byte array
            Marshal.Copy(ptr, rawBytes, 0, 32);

            // the character array is null terminated, so we need to find that terminator
            int nullIndex = Array.IndexOf(rawBytes, (byte)0);

            // finally, since the character array is UTF-8 encoded, get the UTF-8 string
            return Encoding.UTF8.GetString(rawBytes, 0, nullIndex);
        }

    }
}