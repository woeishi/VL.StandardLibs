﻿using ImGuiNET;

using Stride.Graphics;
using Stride.Core.Mathematics;
using Buffer = Stride.Graphics.Buffer;
using Stride.Rendering;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection.Metadata;
using FFmpeg.AutoGen;
using Stride.Core.Extensions;
using VL.Lib.Mathematics;
using System.Diagnostics;

namespace VL.ImGui
{
    /*unsafe*/ partial class ImGuiRenderer
    {
        
        void CheckBuffers(ImDrawDataPtr drawData)
        {
            uint totalVBOSize = (uint)(drawData.TotalVtxCount * Unsafe.SizeOf<ImDrawVert>());
            if (totalVBOSize > vertexBinding.Buffer.SizeInBytes)
            {
                var vertexBuffer = Buffer.Vertex.New(device, (int)(totalVBOSize * 1.5f));
                vertexBinding = new VertexBufferBinding(vertexBuffer, imVertLayout, 0);
            }

            uint totalIBOSize = (uint)(drawData.TotalIdxCount * sizeof(ushort));
            if (totalIBOSize > indexBinding?.Buffer.SizeInBytes)
            {
                var is32Bits = false;
                var indexBuffer = Buffer.Index.New(device, (int)(totalIBOSize * 1.5f));
                indexBinding = new IndexBufferBinding(indexBuffer, is32Bits, 0);
            }
        }

        void UpdateBuffers(ImDrawDataPtr drawData)
        {
            // copy de dators
            int vtxOffsetBytes = 0;
            int idxOffsetBytes = 0;

            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                ImDrawListPtr cmdList = drawData.CmdLists[n];
                vertexBinding.Buffer.SetData(commandList, new DataPointer(cmdList.VtxBuffer.Data, cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>()), vtxOffsetBytes);
                indexBinding.Buffer.SetData(commandList, new DataPointer(cmdList.IdxBuffer.Data, cmdList.IdxBuffer.Size * sizeof(ushort)), idxOffsetBytes);
                vtxOffsetBytes += cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>();
                idxOffsetBytes += cmdList.IdxBuffer.Size * sizeof(ushort);
            }
        }

        void RenderDrawLists(RenderDrawContext context, ImDrawDataPtr drawData)
        {
            CheckBuffers(drawData); // potentially resize buffers first if needed
            UpdateBuffers(drawData); // updeet em now

            // set pipeline stuff
            var is32Bits = false;
            commandList.SetPipelineState(imPipeline);
            commandList.SetVertexBuffer(0, vertexBinding.Buffer, 0, Unsafe.SizeOf<ImDrawVert>());
            commandList.SetIndexBuffer(indexBinding.Buffer, 0, is32Bits);
            imShader.Parameters.Set(ImGuiShader_DrawFXKeys.tex, fontTexture);

            int vtxOffset = 0;
            int idxOffset = 0;
            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                ImDrawListPtr cmdList = drawData.CmdLists[n];

                for (int i = 0; i < cmdList.CmdBuffer.Size; i++)
                {
                    ImDrawCmdPtr cmd = cmdList.CmdBuffer[i];

                    if (cmd.UserCallback != IntPtr.Zero)
                    {
                        // Stride ContextVersion
                        var layer = _context.GetLayer((int)cmd.UserCallback);

                        //// Unsafe Version ... pass RenderLayer
                        //var layer = *(RenderLayer*)cmd.UserCallback;

                        // CallBack FunktionPointer Sample ... see RenderWidget
                        //VL.ImGui.Widgets.RenderWidget.RenderDrawCallback cb = Marshal.GetDelegateForFunctionPointer<VL.ImGui.Widgets.RenderWidget.RenderDrawCallback>(cmd.UserCallback);
                        //if (context != null)
                        //{
                        //    cb(&context, cmdList, cmd);
                        //}

                        if (layer?.Viewport != null)
                        {
                            var renderContext = context?.RenderContext;
                            using (renderContext?.SaveRenderOutputAndRestore())
                            using (renderContext?.SaveViewportAndRestore())
                            {
                                context?.CommandList.SetViewport((Viewport)layer.Viewport);
                                layer.Layer?.Draw(context);
                                context?.CommandList.SetViewport(renderContext.ViewportState.Viewport0);
                            }
                        }
                    }
                    else
                    {
                        if (cmd.TextureId != IntPtr.Zero)
                        {
                            // TODO CHECK THIS ... i think there are allways only one ??
                            // imShader.Parameters.Set(ImGuiShaderKeys.tex, fontTexture);
                        }
                        else
                        {
                            commandList.SetScissorRectangle(
                                new Rectangle(
                                    (int)cmd.ClipRect.X,
                                    (int)cmd.ClipRect.Y,
                                    (int)(cmd.ClipRect.Z - cmd.ClipRect.X),
                                    (int)(cmd.ClipRect.W - cmd.ClipRect.Y)
                                )
                            );

                            imShader.Parameters.Set(ImGuiShader_DrawFXKeys.tex, fontTexture);
                            imShader.Parameters.Set(ImGuiShader_DrawFXKeys.proj, ref projMatrix);
                            imShader.EffectInstance.Apply(graphicsContext);

                            commandList.DrawIndexed((int)cmd.ElemCount, idxOffset, vtxOffset);
                        }

                        idxOffset += (int)cmd.ElemCount;
                    }
                }
                vtxOffset += cmdList.VtxBuffer.Size;
            }
        }
    }
}
