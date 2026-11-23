// Acceso a la cámara del navegador (requiere HTTPS o localhost y el permiso del usuario).
window.gymCamera = {
  stream: null,

  async start(video) {
    try {
      this.stop();
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: "environment", width: { ideal: 1280 }, height: { ideal: 1280 } },
        audio: false
      });
      video.srcObject = this.stream;
      await video.play();
      return true;
    } catch (e) {
      return false;
    }
  },

  capture(video) {
    const maxSide = 1280;
    let w = video.videoWidth, h = video.videoHeight;
    if (!w || !h) return null;
    const scale = Math.min(1, maxSide / Math.max(w, h));
    w = Math.round(w * scale); h = Math.round(h * scale);
    const canvas = document.createElement("canvas");
    canvas.width = w; canvas.height = h;
    canvas.getContext("2d").drawImage(video, 0, 0, w, h);
    return canvas.toDataURL("image/jpeg", 0.85);
  },

  stop() {
    if (this.stream) {
      this.stream.getTracks().forEach(t => t.stop());
      this.stream = null;
    }
  }
};
