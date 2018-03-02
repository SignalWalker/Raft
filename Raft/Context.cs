namespace Raft {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization.Formatters;
    using VulkanCore;
    using VulkanCore.Khr;
    using VulkanCore.Khx;
    using Walker.Data.Geometry.Speed.Rotation;
    using Walker.Data.Geometry.Speed.Space;
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
        public Image[] swapchainImages;
        public ImageWrapper depthBuffer;
        public BufferWrapper uniformBuffer;
        public DescriptorSetLayout descLayout;
        public int width, height;
        public PipelineLayout pipelineLayout;
        public DescriptorPool descriptorPool;
        public DescriptorSet[] descriptorSets;
        public ShaderModule[] shaderStages;
        public ImageView[] imgAttachments;
        public Framebuffer[] framebuffers;
        public BufferWrapper vbo;
        public BufferWrapper index;
        public Pipeline pipeline;
        Semaphore semaphore;
        Primitive cube;

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
            swapchainImages = swapchain.GetImages();
            graphicsBuffers = GraphicsCommandPool.AllocateBuffers(
                new CommandBufferAllocateInfo(CommandBufferLevel.Primary, swapchainImages.Length)).ToList();
            depthBuffer = ImageWrapper.DepthStencil(this, width, height);

            Matrix4F mvp = new Matrix4F(1, 0, 0, 0,
                0, -1, 0, 0,
                0, 0, .5f, 0,
                0, 0, .5f, 1) * Matrix4F.CreatePerspectiveFieldOfView(45, 1, .1f, 100)
                * Matrix4F.LookAt(new Vector3F(0, 3, 10),
                                  new Vector3F(0, 0, 0),
                                  new Vector3F(0, 1, 0))
                * Matrix4F.Identity;

            uniformBuffer = BufferWrapper.DynamicUniform<float>(this, 16);
            IntPtr map = uniformBuffer.memory.Map(0, Interop.SizeOf<float>() * 16);
            Marshal.Copy(mvp.Transpose.Values, 0, map, 16); // transpose because opengl (and, presumably, vulkan) use column-major
            uniformBuffer.memory.Unmap();

            DescriptorSetLayoutBinding dSLBin = new DescriptorSetLayoutBinding(0, DescriptorType.UniformBuffer, 1, ShaderStages.Vertex);
            DescriptorSetLayoutCreateInfo dSLCInfo = new DescriptorSetLayoutCreateInfo(dSLBin);
            descLayout = Device.CreateDescriptorSetLayout(dSLCInfo);
            PipelineLayoutCreateInfo pLCInfo = new PipelineLayoutCreateInfo(new[] {descLayout});
            pipelineLayout = Device.CreatePipelineLayout(pLCInfo);

            DescriptorPoolSize dPSize = new DescriptorPoolSize(DescriptorType.UniformBuffer, 1);
            DescriptorPoolCreateInfo dPInfo = new DescriptorPoolCreateInfo(1, new[] {dPSize});
            descriptorPool = device.CreateDescriptorPool(dPInfo);

            DescriptorSetAllocateInfo dSAInfo = new DescriptorSetAllocateInfo(1, descLayout);
            descriptorSets = descriptorPool.AllocateSets(dSAInfo);

            DescriptorBufferInfo dBInfo = new DescriptorBufferInfo(uniformBuffer.buffer);
            WriteDescriptorSet wDSet = new WriteDescriptorSet(descriptorSets[0], 0, 0, 1,
                DescriptorType.UniformBuffer, null, new[] {dBInfo});
            descriptorPool.UpdateSets(new[] {wDSet});

            AttachmentDescription[] attachments = new AttachmentDescription[2];
            attachments[0] = new AttachmentDescription {
                Format = Format.B8G8R8A8SRgb, // idk what this is supposed to be
                Samples = SampleCounts.Count1, // same ^
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.ColorAttachmentOptimal,
                FinalLayout = ImageLayout.PresentSrcKhr
            };
            attachments[1] = new AttachmentDescription {
                Format = depthBuffer.Format,
                Samples = SampleCounts.Count1, // aaaah
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.DontCare,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.DepthStencilAttachmentOptimal,
                FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
            };

            AttachmentReference color = new AttachmentReference(0, ImageLayout.ColorAttachmentOptimal);
            AttachmentReference depth = new AttachmentReference(1, ImageLayout.DepthStencilAttachmentOptimal);

            SubpassDescription subPass = new SubpassDescription {
                ColorAttachments = new []{color},
                DepthStencilAttachment = depth
            };

            RenderPassCreateInfo passInfo = new RenderPassCreateInfo(new[] {subPass}, attachments);
            RenderPass pass = device.CreateRenderPass(passInfo);

            shaderStages = new[] {
                LoadShaderModule("Resources/Shaders/vert.spv"),
                LoadShaderModule("Resources/Shaders/frag.spv")
            };

            PipelineShaderStageCreateInfo[] shaderStageInfos = {
                new PipelineShaderStageCreateInfo(ShaderStages.Vertex, shaderStages[0], "main"),
                new PipelineShaderStageCreateInfo(ShaderStages.Fragment, shaderStages[1], "main")
            };


            imgAttachments = new ImageView[2];
            imgAttachments[1] = depthBuffer.View;

            List<ImageView> imgViews = swapchainImages.Select(i => i.CreateView(
                                                                  new ImageViewCreateInfo(
                                                                  swapchain.Format,
                                                                  new ImageSubresourceRange(ImageAspects.Color, 0, 1, 0, 1))))
                                           as List<ImageView>;

            framebuffers = new Framebuffer[swapchainImages.Length];
            for (int i = 0; i < framebuffers.Length; i++) {
                imgAttachments[0] = imgViews[i];
                FramebufferCreateInfo fBufferInfo = new FramebufferCreateInfo(imgAttachments, width, height);
                framebuffers[i] = pass.CreateFramebuffer(fBufferInfo);
            }

            cube = Primitive.Box(1,1,1);

            vbo = BufferWrapper.Vertex(this, cube.verts);
            index = BufferWrapper.Index(this, cube.indices);

            PipelineVertexInputStateCreateInfo pVISCInfo = new PipelineVertexInputStateCreateInfo(
                new[] {
                    new VertexInputBindingDescription(0, Interop.SizeOf<Vertex>(), VertexInputRate.Vertex)
                },
                new[] {
                    new VertexInputAttributeDescription(0, 0, Format.R32G32B32SFloat, 0),
                    new VertexInputAttributeDescription(1, 0, Format.R32G32B32SFloat, 24),
                    new VertexInputAttributeDescription(2, 0, Format.R32G32B32SFloat, 12)
                });


            PipelineDynamicStateCreateInfo dStateInfo = new PipelineDynamicStateCreateInfo();
            PipelineInputAssemblyStateCreateInfo iaInfo = new PipelineInputAssemblyStateCreateInfo(PrimitiveTopology.TriangleList);
            PipelineRasterizationStateCreateInfo rsInfo = new PipelineRasterizationStateCreateInfo() {
                PolygonMode = PolygonMode.Fill,
                CullMode = CullModes.Back,
                FrontFace = FrontFace.CounterClockwise,
                DepthClampEnable = true,
                RasterizerDiscardEnable = false,
                DepthBiasClamp = 0,
                DepthBiasConstantFactor = 0,
                DepthBiasEnable = false,
                DepthBiasSlopeFactor = 0,
                LineWidth = 1f
            };
            PipelineColorBlendStateCreateInfo cbInfo = new PipelineColorBlendStateCreateInfo(new [] { new PipelineColorBlendAttachmentState {
                ColorWriteMask = ColorComponents.R | ColorComponents.G | ColorComponents.B,
                BlendEnable = false
            } });
            PipelineViewportStateCreateInfo vpInfo = new PipelineViewportStateCreateInfo(
                new Viewport(0, 0, width, height),
                new Rect2D(0, 0, width, height));
            PipelineDepthStencilStateCreateInfo dsInfo = new PipelineDepthStencilStateCreateInfo {
                DepthTestEnable = true,
                DepthWriteEnable = true,
                DepthCompareOp = CompareOp.LessOrEqual,
                DepthBoundsTestEnable = false,
                StencilTestEnable = false
            };
            dsInfo.Back.FailOp = StencilOp.Keep;
            dsInfo.Back.PassOp = StencilOp.Keep;
            dsInfo.Back.CompareOp = CompareOp.Always;
            dsInfo.Back.CompareMask = 0;
            dsInfo.Back.Reference = 0;
            dsInfo.Back.DepthFailOp = StencilOp.Keep;
            dsInfo.Back.WriteMask = 0;
            dsInfo.Front = dsInfo.Back;
            PipelineMultisampleStateCreateInfo msInfo = new PipelineMultisampleStateCreateInfo {
                SampleMask = null,
                RasterizationSamples = SampleCounts.Count1,
                SampleShadingEnable = false,
                AlphaToCoverageEnable = false,
                AlphaToOneEnable = false,
                MinSampleShading = 0
            };

            GraphicsPipelineCreateInfo pInfo = new GraphicsPipelineCreateInfo(pipelineLayout, pass, 0, shaderStageInfos, iaInfo, pVISCInfo, rsInfo, null, vpInfo, msInfo, dsInfo, cbInfo, null, PipelineCreateFlags.None, null, 0);
            pipeline = device.CreateGraphicsPipeline(pInfo);

            semaphore = device.CreateSemaphore();

            int n = device.AcquireNextImage2Khx(new AcquireNextImageInfoKhx(swapchain, int.MaxValue, semaphore));

            RecordCommandBuffer(this.graphicsBuffers[0], n);


        }

        public void RecordCommandBuffer(CommandBuffer cmdBuffer, int imgIndex) {
            RenderPassBeginInfo bInfo = new RenderPassBeginInfo(
                framebuffers[imgIndex],
                new Rect2D(0, 0, width, height),
                new ClearColorValue(new ColorF4(.2f, .2f, .2f, 1)),
                new ClearDepthStencilValue(1, 0));
            cmdBuffer.CmdBeginRenderPass(bInfo);
            cmdBuffer.CmdBindDescriptorSets(PipelineBindPoint.Graphics, pipelineLayout, 0, descriptorSets);
            cmdBuffer.CmdBindPipeline(PipelineBindPoint.Graphics, pipeline);
            cmdBuffer.CmdBindVertexBuffer(vbo.buffer);
            cmdBuffer.CmdBindIndexBuffer(index.buffer);
            cmdBuffer.CmdSetViewport(new Viewport(0, 0, width, height));
            cmdBuffer.CmdDrawIndexed(index.count);
            cmdBuffer.CmdEndRenderPass();
        }

        public ShaderModule LoadShaderModule(string path)
        {
            const int defaultBufferSize = 4096;
            using (Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms, defaultBufferSize);
                return device.CreateShaderModule(new ShaderModuleCreateInfo(ms.ToArray()));
            }
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
                    AllocationSize = memReq.Size,
                    MemoryTypeIndex = memoryProperties.MemoryTypes.IndexOf(memReq.MemoryTypeBits,
                        VulkanCore.MemoryProperties.DeviceLocal)
                };
                DeviceMemory mem = Device.AllocateMemory(mInfo);
                res.BindMemory(mem);
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
                MemoryRequirements memReq = res.GetMemoryRequirements();
                MemoryAllocateInfo mInfo = new MemoryAllocateInfo {
                    AllocationSize = memReq.Size,
                    MemoryTypeIndex = memoryProperties.MemoryTypes.IndexOf(
                        memReq.MemoryTypeBits,
                        VulkanCore.MemoryProperties.DeviceLocal)
                };
                DeviceMemory mem = Device.AllocateMemory(mInfo);
                mem.Map(0, memReq.Size);

                mem.Unmap();
                res.BindMemory(mem);
            }

            return res;
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