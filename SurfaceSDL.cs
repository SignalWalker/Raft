namespace Raft {
    using System;
    using VulkanCore;
    using VulkanCore.Khr;
    public class SurfaceSDL : SurfaceKhr {

        protected internal SurfaceSDL(Instance parent, ref AllocationCallbacks? allocator, IntPtr handle) : base(parent, ref allocator, (long) handle) {
            Console.Out.WriteLine(handle);
            Console.Out.WriteLine(Handle);
        }

    }
}