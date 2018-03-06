using System;

namespace Raft {
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using SDL2;
    using VulkanCore;
    using VulkanCore.Khr;
    using VulkanCubes;

    public class Forge {
        static bool SDLInit;
        const int EngineVersion = 1;
        public readonly Instance instance;
        public List<Window> windows = new List<Window>();

        public Context Context(int winIndex) => windows[winIndex].GetContext(instance);

        public Forge(int appVersion, string name, int winX, int winY, int winWidth, int winHeight) {

            ApplicationInfo appInfo = new ApplicationInfo {
                ApplicationName = name,
                ApplicationVersion = appVersion,
                EngineName = "Raft",
                EngineVersion = EngineVersion,
                ApiVersion = new Version(1, 0, 61)
            };

            if (!SDLInit) { InitSDL(); }

            windows.Add(new Window(name, winX, winY, winWidth, winHeight));

            IntPtr[] pNames = new IntPtr[0];
            // you have to call this twice because something, somewhere, is terrible
            SDL.SDL_Vulkan_GetInstanceExtensions(windows[0].handle, out uint pCount, null);
            pNames = new IntPtr[pCount];
            SDL.SDL_Vulkan_GetInstanceExtensions(windows[0].handle, out pCount, pNames);

            InstanceCreateInfo instInfo = new InstanceCreateInfo {
                ApplicationInfo = appInfo,
                EnabledExtensionNames = pNamesToStrings(pNames)
            };

            instance = new Instance(instInfo);
        }

        public void Quit() {
            SDL.SDL_Quit();
            foreach (Window window in windows) { DestroyWindow(window); }
            windows.Clear();
        }

        public Window MakeWindow(string title, int x, int y, int width, int height) {
            Window res = new Window(title, x, y, width, height);
            windows.Add(res);
            return res;
        }

        public void DestroyWindow(Window win) {
            win.Destroy();
        }

        public void DestroyWindow(int index) {
            windows[index].Destroy();
            windows.RemoveAt(index);
        }

        public static void InitSDL() {
            if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) != 0) { throw new SDLException(); }
            SDLInit = true;
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