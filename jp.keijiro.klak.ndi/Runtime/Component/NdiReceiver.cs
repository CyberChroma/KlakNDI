using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using IntPtr = System.IntPtr;
using Marshal = System.Runtime.InteropServices.Marshal;
using UnityEngine.Rendering;

namespace Klak.Ndi {

[ExecuteInEditMode]
public sealed partial class NdiReceiver : MonoBehaviour
{
    public static bool isConnected = false;

    #region Receiver objects

    Interop.Recv _recv;
    FormatConverter _converter;
    MaterialPropertyBlock _override;

    [SerializeField] Material _rgbConversionMaterial;
    [SerializeField] Material _maskConversionMaterial;
    [SerializeField] RenderTexture maskRenderTexture;

    private Material _colorConversionMaterialInstance = null;
    private Material _maskConversionMaterialInstance = null;
    private Texture2D _recvTexture = null;
    private CommandBuffer _blitCommandBuffer = null;

    void PrepareReceiverObjects()
    {
        if (_recv == null) _recv = RecvHelper.TryCreateRecv(ndiName);
        if (_converter == null) _converter = new FormatConverter(_resources);
        if (_override == null) _override = new MaterialPropertyBlock();
    }

    void ReleaseReceiverObjects()
    {
        _recv?.Dispose();
        _recv = null;

        _converter?.Dispose();
        _converter = null;

        // We don't dispose _override because it's reusable.
    }

    #endregion

    #region Receiver implementation

    RenderTexture TryReceiveFrame()
    {
        PrepareReceiverObjects();
        if (_recv == null) {
            isConnected = false;
            return null;
        }

        // Video frame capturing
        var frameOrNull = RecvHelper.TryCaptureVideoFrame(_recv);
        if (frameOrNull == null) {
            isConnected = false;
            return null;
        }
        var frame = (Interop.VideoFrame)frameOrNull;

        // Pixel format conversion
        var rt = _converter.Decode
          (frame.Width, frame.Height, Util.HasAlpha(frame.FourCC), frame.Data);

        // Metadata retrieval
        if (frame.Metadata != IntPtr.Zero)
            metadata = Marshal.PtrToStringAnsi(frame.Metadata);
        else
            metadata = null;

        UpdateRenderPipeline((uint)frame.Width, (uint)frame.Height, false, rt.GetNativeTexturePtr());
        Graphics.ExecuteCommandBuffer(_blitCommandBuffer);

        // Video frame release
        _recv.FreeVideoFrame(frame);
        isConnected = true;
        return rt;
    }

    #endregion

    #region Component state controller

    internal void Restart() => ReleaseReceiverObjects();

    #endregion

    #region MonoBehaviour implementation

    void OnDisable() => ReleaseReceiverObjects();

    void Update()
    {
        var rt = TryReceiveFrame();
        if (rt == null) return;

        // Material property override
        if (targetRenderer != null)
        {
            targetRenderer.GetPropertyBlock(_override);
            _override.SetTexture(targetMaterialProperty, rt);
            targetRenderer.SetPropertyBlock(_override);
        }

        // External texture update
        if (targetTexture != null) Graphics.Blit(rt, targetTexture);
    }

    void UpdateRenderPipeline(uint texWidth, uint texHeight, bool isYUV, System.IntPtr texturePtr) {
        if (_recvTexture && _recvTexture.width == texWidth && _recvTexture.height == texHeight) {
            _recvTexture.UpdateExternalTexture(texturePtr);
            return;
        }

        if (_recvTexture) {
            Destroy(_recvTexture);
            _recvTexture = null;
        }

        // Resize the render pipeline to match
        int videoWidth = (int)texWidth;
        int videoHeight = (int)texHeight;

        //Debug.LogFormat(this, "Format Changed on NDI Source {0}. Resolution {1} x {2}. YUV = {3}", source?.name?.managedString, texWidth, texHeight, isYUV);
        _recvTexture = Texture2D.CreateExternalTexture(videoWidth, videoHeight, TextureFormat.ARGB32, false, false, texturePtr);

        if (_colorConversionMaterialInstance)
            Destroy(_colorConversionMaterialInstance);

        // Create the color conversion material
        Material colorConversionMaterial = _rgbConversionMaterial;
        if (colorConversionMaterial) {
            _colorConversionMaterialInstance = new Material(colorConversionMaterial);
            _colorConversionMaterialInstance.name += " (Instance)";
        }

        // Have to setup the command buffer again since some of our textures changed
        SetupCommandBuffer();
    }

    void SetupCommandBuffer() {
        if (_blitCommandBuffer != null) {
            _blitCommandBuffer.Dispose();
            _blitCommandBuffer = null;
        }

        if (!_recvTexture)
            return;

        _blitCommandBuffer = new CommandBuffer();
        _blitCommandBuffer.name = this.name;

        var currentTextureTarget = new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive);
        var maskTextureTarget = new RenderTargetIdentifier(maskRenderTexture);

        if (_maskConversionMaterial) {
            if (_maskConversionMaterialInstance == null) {
                _maskConversionMaterialInstance = new Material(_maskConversionMaterial);
                _maskConversionMaterialInstance.DisableKeyword("_FLIPTEXTURE_ON");
                _maskConversionMaterialInstance.EnableKeyword("_FLIPTEXTURE_OFF");
            }
            _blitCommandBuffer.Blit(_recvTexture, maskTextureTarget, _maskConversionMaterialInstance);
        } else {
            _blitCommandBuffer.SetRenderTarget(maskTextureTarget);
            _blitCommandBuffer.ClearRenderTarget(true, true, Color.white);
        }

        _blitCommandBuffer.SetRenderTarget(currentTextureTarget);
    }

#endregion
}

} // namespace Klak.Ndi
