using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

/// <summary>Renders carried items after scene geometry without depth occlusion.</summary>
[Serializable]
public sealed class HeldItemOverlayPass : CustomPass
{
    public LayerMask layerMask;

    protected override void Execute(CustomPassContext context)
    {
        RenderStateBlock renderState = new RenderStateBlock(RenderStateMask.Depth)
        {
            depthState = new DepthState(false, CompareFunction.Always)
        };

        CustomPassUtils.DrawRenderers(
            context,
            layerMask,
            CustomPass.RenderQueueType.All,
            overrideRenderState: renderState
        );
    }
}
