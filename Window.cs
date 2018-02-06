namespace Raft {
    using System;
    using SDL2;
    using VulkanCore;
    using VulkanCore.Khr;
    using VulkanCubes;

    public class Window {

        static bool SDLInit = false;

        protected internal IntPtr handle;
        string title;
        SurfaceKhr surf;
        Context context;

        internal Window(string title, int x, int y, int width, int height) {
            this.title = title;
            handle = SDL.SDL_CreateWindow(title, x, y, width, height, SDL.SDL_WindowFlags.SDL_WINDOW_VULKAN
                                                                    | SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
            if (handle == IntPtr.Zero) { throw new SDLException(); }
        }

        internal void Destroy() {
            context.Dispose();
            surf.Dispose();
            SDL.SDL_DestroyWindow(handle);
        }

        public SurfaceKhr GetSurface(Instance inst) => surf ?? (surf = MakeSurface(inst));

        public Context GetContext(Instance inst) => context ?? (context = new Context(inst, GetSurface(inst)));

        SurfaceKhr MakeSurface(Instance inst) {
            switch (SDL.SDL_GetPlatform()) {
                case "Windows":
                    return inst.CreateWin32SurfaceKhr(new Win32SurfaceCreateInfoKhr(inst, handle));
                case "Linux":
                    SDL.SDL_Vulkan_CreateSurface(handle, inst, out IntPtr surfHandle);
                    AllocationCallbacks? b = null;
                    return new SurfaceKhr(inst, ref b, (long) surfHandle);
                default:
                    throw new NotImplementedException();
            }
        }



    }
}