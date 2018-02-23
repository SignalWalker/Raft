namespace Raft {
    using System;
    using VulkanCore;
    using Buffer = VulkanCore.Buffer;

    public class BufferWrapper : IDisposable {

        public Buffer buffer;
        public DeviceMemory memory;
        public int count;

        BufferWrapper(Buffer buffer, DeviceMemory mem, int count) {
            this.buffer = buffer;
            this.memory = mem;
            this.count = count;
        }

        public void Dispose() {
            memory.Dispose();
            buffer.Dispose();
        }

        public static BufferWrapper DynamicUniform<T>(Context ctx, int count) where T : struct {
            long size = Interop.SizeOf<T>() * count;

            Buffer buffer = ctx.Device.CreateBuffer(new BufferCreateInfo(size, BufferUsages.UniformBuffer));
            MemoryRequirements memoryRequirements = buffer.GetMemoryRequirements();
            // We require host visible memory so we can map it and write to it directly.
            // We require host coherent memory so that writes are visible to the GPU right after unmapping it.
            int memoryTypeIndex = ctx.MemoryProperties.MemoryTypes.IndexOf(
                memoryRequirements.MemoryTypeBits,
                MemoryProperties.HostVisible | MemoryProperties.HostCoherent);
            DeviceMemory memory = ctx.Device.AllocateMemory(new MemoryAllocateInfo(memoryRequirements.Size, memoryTypeIndex));
            buffer.BindMemory(memory);

            return new BufferWrapper(buffer, memory, count);
        }

        public static BufferWrapper Index(Context ctx, int[] indices) {
            long size = indices.Length * sizeof(int);

            // Create staging buffer.
            Buffer stagingBuffer = ctx.Device.CreateBuffer(new BufferCreateInfo(size, BufferUsages.TransferSrc));
            MemoryRequirements stagingReq = stagingBuffer.GetMemoryRequirements();
            int stagingMemoryTypeIndex = ctx.MemoryProperties.MemoryTypes.IndexOf(
                stagingReq.MemoryTypeBits,
                MemoryProperties.HostVisible | MemoryProperties.HostCoherent);
            DeviceMemory stagingMemory = ctx.Device.AllocateMemory(new MemoryAllocateInfo(stagingReq.Size, stagingMemoryTypeIndex));
            IntPtr indexPtr = stagingMemory.Map(0, stagingReq.Size);
            Interop.Write(indexPtr, indices);
            stagingMemory.Unmap();
            stagingBuffer.BindMemory(stagingMemory);

            // Create a device local buffer.
            Buffer buffer = ctx.Device.CreateBuffer(new BufferCreateInfo(size, BufferUsages.IndexBuffer | BufferUsages.TransferDst));
            MemoryRequirements req = buffer.GetMemoryRequirements();
            int memoryTypeIndex = ctx.MemoryProperties.MemoryTypes.IndexOf(
                req.MemoryTypeBits,
                MemoryProperties.DeviceLocal);
            DeviceMemory memory = ctx.Device.AllocateMemory(new MemoryAllocateInfo(req.Size, memoryTypeIndex));
            buffer.BindMemory(memory);

            // Copy the data from staging buffer to device local buffer.
            CommandBuffer cmdBuffer = ctx.GraphicsCommandPool.AllocateBuffers(new CommandBufferAllocateInfo(CommandBufferLevel.Primary, 1))[0];
            cmdBuffer.Begin(new CommandBufferBeginInfo(CommandBufferUsages.OneTimeSubmit));
            cmdBuffer.CmdCopyBuffer(stagingBuffer, buffer, new BufferCopy(size));
            cmdBuffer.End();

            // Submit.
            Fence fence = ctx.Device.CreateFence();
            ctx.GraphicsQueue.Submit(new SubmitInfo(commandBuffers: new[] { cmdBuffer }), fence);
            fence.Wait();

            // Cleanup.
            fence.Dispose();
            cmdBuffer.Dispose();
            stagingBuffer.Dispose();
            stagingMemory.Dispose();

            return new BufferWrapper(buffer, memory, indices.Length);
        }

        public static BufferWrapper Vertex<T>(Context ctx, T[] vertices) where T : struct
        {
            long size = vertices.Length * Interop.SizeOf<T>();

            // Create a staging buffer that is writable by host.
            Buffer stagingBuffer = ctx.Device.CreateBuffer(new BufferCreateInfo(size, BufferUsages.TransferSrc));
            MemoryRequirements stagingReq = stagingBuffer.GetMemoryRequirements();
            int stagingMemoryTypeIndex = ctx.MemoryProperties.MemoryTypes.IndexOf(
                stagingReq.MemoryTypeBits,
                MemoryProperties.HostVisible | MemoryProperties.HostCoherent);
            DeviceMemory stagingMemory = ctx.Device.AllocateMemory(new MemoryAllocateInfo(stagingReq.Size, stagingMemoryTypeIndex));
            IntPtr vertexPtr = stagingMemory.Map(0, stagingReq.Size);
            Interop.Write(vertexPtr, vertices);
            stagingMemory.Unmap();
            stagingBuffer.BindMemory(stagingMemory);

            // Create a device local buffer where the vertex data will be copied and which will be used for rendering.
            Buffer buffer = ctx.Device.CreateBuffer(new BufferCreateInfo(size, BufferUsages.VertexBuffer | BufferUsages.TransferDst));
            MemoryRequirements req = buffer.GetMemoryRequirements();
            int memoryTypeIndex = ctx.MemoryProperties.MemoryTypes.IndexOf(
                req.MemoryTypeBits,
                MemoryProperties.DeviceLocal);
            DeviceMemory memory = ctx.Device.AllocateMemory(new MemoryAllocateInfo(req.Size, memoryTypeIndex));
            buffer.BindMemory(memory);

            // Copy the data from staging buffers to device local buffers.
            CommandBuffer cmdBuffer = ctx.GraphicsCommandPool.AllocateBuffers(new CommandBufferAllocateInfo(CommandBufferLevel.Primary, 1))[0];
            cmdBuffer.Begin(new CommandBufferBeginInfo(CommandBufferUsages.OneTimeSubmit));
            cmdBuffer.CmdCopyBuffer(stagingBuffer, buffer, new BufferCopy(size));
            cmdBuffer.End();

            // Submit.
            Fence fence = ctx.Device.CreateFence();
            ctx.GraphicsQueue.Submit(new SubmitInfo(commandBuffers: new[] { cmdBuffer }), fence);
            fence.Wait();

            // Cleanup.
            fence.Dispose();
            cmdBuffer.Dispose();
            stagingBuffer.Dispose();
            stagingMemory.Dispose();

            return new BufferWrapper(buffer, memory, vertices.Length);
        }

        public static BufferWrapper Storage<T>(Context ctx, T[] data) where T : struct
        {
            long size = data.Length * Interop.SizeOf<T>();

            // Create a staging buffer that is writable by host.
            Buffer stagingBuffer = ctx.Device.CreateBuffer(new BufferCreateInfo(size, BufferUsages.TransferSrc));
            MemoryRequirements stagingReq = stagingBuffer.GetMemoryRequirements();
            int stagingMemoryTypeIndex = ctx.MemoryProperties.MemoryTypes.IndexOf(
                stagingReq.MemoryTypeBits,
                MemoryProperties.HostVisible | MemoryProperties.HostCoherent);
            DeviceMemory stagingMemory = ctx.Device.AllocateMemory(new MemoryAllocateInfo(stagingReq.Size, stagingMemoryTypeIndex));
            IntPtr vertexPtr = stagingMemory.Map(0, stagingReq.Size);
            Interop.Write(vertexPtr, data);
            stagingMemory.Unmap();
            stagingBuffer.BindMemory(stagingMemory);

            // Create a device local buffer where the data will be copied.
            Buffer buffer = ctx.Device.CreateBuffer(new BufferCreateInfo(size, BufferUsages.VertexBuffer | BufferUsages.StorageBuffer | BufferUsages.TransferDst));
            MemoryRequirements req = buffer.GetMemoryRequirements();
            int memoryTypeIndex = ctx.MemoryProperties.MemoryTypes.IndexOf(
                req.MemoryTypeBits,
                MemoryProperties.DeviceLocal);
            DeviceMemory memory = ctx.Device.AllocateMemory(new MemoryAllocateInfo(req.Size, memoryTypeIndex));
            buffer.BindMemory(memory);

            // Copy the data from staging buffers to device local buffers.
            CommandBuffer cmdBuffer = ctx.GraphicsCommandPool.AllocateBuffers(new CommandBufferAllocateInfo(CommandBufferLevel.Primary, 1))[0];
            cmdBuffer.Begin(new CommandBufferBeginInfo(CommandBufferUsages.OneTimeSubmit));
            cmdBuffer.CmdCopyBuffer(stagingBuffer, buffer, new BufferCopy(size));
            cmdBuffer.End();

            // Submit.
            Fence fence = ctx.Device.CreateFence();
            ctx.GraphicsQueue.Submit(new SubmitInfo(commandBuffers: new[] { cmdBuffer }), fence);
            fence.Wait();

            // Cleanup.
            fence.Dispose();
            cmdBuffer.Dispose();
            stagingBuffer.Dispose();
            stagingMemory.Dispose();

            return new BufferWrapper(buffer, memory, data.Length);
        }

    }
}