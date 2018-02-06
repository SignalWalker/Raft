namespace Raft {
    using System;
    using VulkanCore;
    using VulkanCore.Khr;

    public class Context {
        readonly PhysicalDevice physicalDevice;
        readonly Device device;
        readonly PhysicalDeviceMemoryProperties memoryProperties;
        readonly PhysicalDeviceFeatures features;
        readonly PhysicalDeviceProperties properties;
        readonly Queue graphicsQueue;
        readonly Queue computeQueue;
        readonly Queue presentQueue;
        readonly CommandPool graphicsCommandPool;
        readonly CommandPool computeCommandPool;

        public PhysicalDevice PhysicalDevice => physicalDevice;
        public Device Device => device;
        public PhysicalDeviceMemoryProperties MemoryProperties => memoryProperties;
        public PhysicalDeviceFeatures Features => features;
        public PhysicalDeviceProperties Properties => properties;
        public Queue GraphicsQueue => graphicsQueue;
        public Queue ComputeQueue => computeQueue;
        public Queue PresentQueue => presentQueue;
        public CommandPool GraphicsCommandPool => graphicsCommandPool;
        public CommandPool ComputeCommandPool => computeCommandPool;

        public Context(Instance inst, SurfaceKhr surf) {

            // Find graphics and presentation capable physical device(s) that support
            // the provided surface for platform.
            int graphicsQueueFamilyIndex = -1;
            int computeQueueFamilyIndex = -1;
            int presentQueueFamilyIndex = -1;
            foreach (PhysicalDevice physDev in inst.EnumeratePhysicalDevices()) {
                QueueFamilyProperties[] queueFamilyProperties = physDev.GetQueueFamilyProperties();
                for (int i = 0; i < queueFamilyProperties.Length; i++) {
                    if (queueFamilyProperties[i].QueueFlags.HasFlag(Queues.Graphics)) {
                        if (graphicsQueueFamilyIndex == -1) graphicsQueueFamilyIndex = i;
                        if (computeQueueFamilyIndex == -1) computeQueueFamilyIndex = i;

                        if (physDev.GetSurfaceSupportKhr(i, surf)) { presentQueueFamilyIndex = i; }

                        if (graphicsQueueFamilyIndex != -1 &&
                            computeQueueFamilyIndex != -1 &&
                            presentQueueFamilyIndex != -1) {
                            physicalDevice = physDev;
                            break;
                        }
                    }
                }

                if (PhysicalDevice != null) break;
            }

            if (PhysicalDevice == null) { throw new InvalidOperationException("No suitable physical device found."); }

            // Store memory properties of the physical device.
            memoryProperties = PhysicalDevice.GetMemoryProperties();
            features = PhysicalDevice.GetFeatures();
            properties = PhysicalDevice.GetProperties();

            // Create a logical device.
            bool sameGraphicsAndPresent = graphicsQueueFamilyIndex == presentQueueFamilyIndex;
            var queueCreateInfos = new DeviceQueueCreateInfo[sameGraphicsAndPresent ? 1 : 2];
            queueCreateInfos[0] = new DeviceQueueCreateInfo(graphicsQueueFamilyIndex, 1, 1.0f);
            if (!sameGraphicsAndPresent)
                queueCreateInfos[1] = new DeviceQueueCreateInfo(presentQueueFamilyIndex, 1, 1.0f);

            var deviceCreateInfo = new DeviceCreateInfo(
                queueCreateInfos,
                new[] {Constant.DeviceExtension.KhrSwapchain},
                Features);
            device = PhysicalDevice.CreateDevice(deviceCreateInfo);

            // Get queue(s).
            graphicsQueue = Device.GetQueue(graphicsQueueFamilyIndex);
            computeQueue = computeQueueFamilyIndex == graphicsQueueFamilyIndex
                               ? GraphicsQueue
                               : Device.GetQueue(computeQueueFamilyIndex);
            presentQueue = presentQueueFamilyIndex == graphicsQueueFamilyIndex
                               ? GraphicsQueue
                               : Device.GetQueue(presentQueueFamilyIndex);

            // Create command pool(s).
            graphicsCommandPool = Device.CreateCommandPool(new CommandPoolCreateInfo(graphicsQueueFamilyIndex));
            computeCommandPool = Device.CreateCommandPool(new CommandPoolCreateInfo(computeQueueFamilyIndex));
        }

        public void Dispose() {
            ComputeCommandPool.Dispose();
            GraphicsCommandPool.Dispose();
            Device.Dispose();
        }
    }
}