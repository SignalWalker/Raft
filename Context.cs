namespace Raft {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization.Formatters;
    using VulkanCore;
    using VulkanCore.Khr;
    using Buffer = VulkanCore.Buffer;

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
        public List<CommandBuffer> graphicsBuffers;
        public List<CommandBuffer> computeBuffers;
        public SwapchainKhr swapchain;
        public List<Image> swapchainImages;
        public Image depthBuffer;
        public Buffer uniformBuffer;
        public int width, height;

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

            swapchain = CreateSwapchain(surf);
            MakeBuffers();
            depthBuffer = MakeDepthBuffer(true);
            uniformBuffer = MakeUniformBuffer();
        }

        void MakeBuffers() {
            graphicsBuffers = GraphicsCommandPool.AllocateBuffers(
                new CommandBufferAllocateInfo(CommandBufferLevel.Primary, swapchainImages.Count)).ToList();
        }

        SwapchainKhr CreateSwapchain(SurfaceKhr surface) {
            SurfaceCapabilitiesKhr capabilities = PhysicalDevice.GetSurfaceCapabilitiesKhr(surface);
            SurfaceFormatKhr[] formats = PhysicalDevice.GetSurfaceFormatsKhr(surface);
            PresentModeKhr[] presentModes = PhysicalDevice.GetSurfacePresentModesKhr(surface);
            Format format = formats.Length == 1 && formats[0].Format == Format.Undefined
                                ? Format.B8G8R8A8UNorm
                                : formats[0].Format;
            PresentModeKhr presentMode =
                presentModes.Contains(PresentModeKhr.Mailbox) ? PresentModeKhr.Mailbox :
                presentModes.Contains(PresentModeKhr.FifoRelaxed) ? PresentModeKhr.FifoRelaxed :
                presentModes.Contains(PresentModeKhr.Fifo) ? PresentModeKhr.Fifo :
                PresentModeKhr.Immediate;

            return Device.CreateSwapchainKhr(new SwapchainCreateInfoKhr(
                surface,
                format,
                capabilities.CurrentExtent,
                capabilities.CurrentTransform,
                presentMode));
        }

        Image MakeDepthBuffer(bool bindMemory = false) {
            ImageCreateInfo info = new ImageCreateInfo {
                ImageType = ImageType.Image2D,
                Format = Format.D16UNorm,
                Extent = new Extent3D(width, height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Samples = SampleCounts.Count1, // this is a mystery
                InitialLayout = ImageLayout.Undefined,
                Usage = ImageUsages.DepthStencilAttachment,
                QueueFamilyIndices = null,
                SharingMode = SharingMode.Exclusive,
                Flags = 0
            };
            Image res = Device.CreateImage(info);
            if (bindMemory) {
                MemoryRequirements memReq = res.GetMemoryRequirements();
                MemoryAllocateInfo mInfo = new MemoryAllocateInfo {
                    AllocationSize = res.GetMemoryRequirements().Size,
                    MemoryTypeIndex = memoryProperties.MemoryTypes.IndexOf(memReq.MemoryTypeBits,
                        VulkanCore.MemoryProperties.DeviceLocal)
                };
                DeviceMemory mem = Device.AllocateMemory(mInfo);
                depthBuffer.BindMemory(mem);
            }
            return res;
        }

        Buffer MakeUniformBuffer(long size, bool bindMemory = false) {
            BufferCreateInfo bInfo = new BufferCreateInfo {
                Usage = BufferUsages.UniformBuffer,
                Size = size,
                QueueFamilyIndices = null,
                SharingMode = SharingMode.Exclusive,
                Flags = 0
            };
            Buffer res = Device.CreateBuffer(bInfo);
            if (bindMemory) {

            }
        }

        public void Dispose() {
            ComputeCommandPool.Dispose();
            GraphicsCommandPool.Dispose();
            swapchain.Dispose();
            graphicsBuffers.ForEach(b => b.Dispose());
            computeBuffers.ForEach(b => b.Dispose());
            Device.Dispose();
        }
    }
}