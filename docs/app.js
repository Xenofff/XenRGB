/**
 * XenRGB — Liquid Glass Landing Page & 2D Rig Simulator
 */

document.addEventListener('DOMContentLoaded', () => {
  initRigSimulator();
  initGlassCardSpotlights();
  initCopyButtons();
  initMobileNav();
});

/* ==========================================================================
   2D Spatial Rig Simulator Canvas
   ========================================================================== */

function initRigSimulator() {
  const canvas = document.getElementById('rig-canvas');
  if (!canvas) return;
  const ctx = canvas.getContext('2d');

  let currentEffect = 'cotton-candy';
  let mousePos = { x: -1000, y: -1000, active: false };
  let lastTime = performance.now();
  let frameCount = 0;
  let fpsTimer = performance.now();

  const fpsEl = document.getElementById('sim-fps');

  // Simulated Peripherals Geometry (Normalized 0..1 coordinates mapped to canvas)
  const W = canvas.width;
  const H = canvas.height;

  // 1. Keyboard Layout (6 rows x 16 keys)
  const keyboard = [];
  const kbX = 60, kbY = 130, kbW = 460, kbH = 200;
  const kbRows = 6, kbCols = 16;
  for (let r = 0; r < kbRows; r++) {
    for (let c = 0; c < kbCols; c++) {
      const kw = (kbW - (kbCols - 1) * 4) / kbCols;
      const kh = (kbH - (kbRows - 1) * 4) / kbRows;
      const kx = kbX + c * (kw + 4);
      const ky = kbY + r * (kh + 4);
      keyboard.push({ x: kx + kw / 2, y: ky + kh / 2, w: kw, h: kh, rx: 3 });
    }
  }

  // 2. Gaming Mouse (Body + Wheel + Ring)
  const mouseX = 570, mouseY = 210;
  const mouseLeds = [];
  // Mouse perimeter ring
  for (let i = 0; i < 16; i++) {
    const angle = (i / 16) * Math.PI * 2;
    mouseLeds.push({
      x: mouseX + Math.cos(angle) * 32,
      y: mouseY + Math.sin(angle) * 52,
      size: 5
    });
  }
  // Scroll wheel & logo
  mouseLeds.push({ x: mouseX, y: mouseY - 25, size: 7 }); // Wheel
  mouseLeds.push({ x: mouseX, y: mouseY + 15, size: 10 }); // Center Logo

  // 3. Triple Radiator Fans (Concentric LED Rings)
  const fans = [];
  const fanCenters = [
    { x: 740, y: 130 },
    { x: 740, y: 250 },
    { x: 740, y: 370 }
  ];
  fanCenters.forEach(fc => {
    const leds = [];
    const ringRadius = 42;
    const ringCount = 12;
    for (let i = 0; i < ringCount; i++) {
      const a = (i / ringCount) * Math.PI * 2;
      leds.push({
        x: fc.x + Math.cos(a) * ringRadius,
        y: fc.y + Math.sin(a) * ringRadius,
        size: 6
      });
    }
    // Hub leds
    leds.push({ x: fc.x, y: fc.y, size: 8 });
    fans.push({ center: fc, leds });
  });

  // 4. Ambient Desk LED Strip (Top border)
  const deskStrip = [];
  const stripCount = 48;
  for (let i = 0; i < stripCount; i++) {
    deskStrip.push({
      x: 50 + (i / (stripCount - 1)) * 860,
      y: 40,
      size: 5
    });
  }

  // Cotton Candy Blob Simulation
  const blobs = [];
  const blobCount = 18;
  for (let i = 0; i < blobCount; i++) {
    blobs.push({
      x: Math.random() * W,
      y: Math.random() * H,
      radius: 60 + Math.random() * 50,
      vx: (Math.random() - 0.5) * 1.2,
      vy: (Math.random() - 0.5) * 1.2,
      isCyan: Math.random() > 0.4
    });
  }

  // Mouse interactivity on Canvas
  canvas.addEventListener('mousemove', (e) => {
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;
    mousePos.x = (e.clientX - rect.left) * scaleX;
    mousePos.y = (e.clientY - rect.top) * scaleY;
    mousePos.active = true;
  });

  canvas.addEventListener('mouseleave', () => {
    mousePos.active = false;
  });

  // Effect Switcher Buttons
  const effectButtons = document.querySelectorAll('#effect-btn-group .effect-chip');
  effectButtons.forEach(btn => {
    btn.addEventListener('click', () => {
      effectButtons.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      currentEffect = btn.dataset.effect;
    });
  });

  // Main Render Loop
  function render(time) {
    const dt = (time - lastTime) / 1000;
    lastTime = time;

    // FPS Meter
    frameCount++;
    if (time - fpsTimer >= 500) {
      const fps = (frameCount / ((time - fpsTimer) / 1000)).toFixed(1);
      if (fpsEl) fpsEl.textContent = fps;
      frameCount = 0;
      fpsTimer = time;
    }

    // Update Blobs for Cotton Candy
    blobs.forEach(b => {
      b.x += b.vx;
      b.y += b.vy;
      if (b.x < -80) b.x = W + 80;
      if (b.x > W + 80) b.x = -80;
      if (b.y < -80) b.y = H + 80;
      if (b.y > H + 80) b.y = -80;
    });

    // Clear background (Dark desk surface)
    ctx.fillStyle = '#07090e';
    ctx.fillRect(0, 0, W, H);

    // Draw Subtle Grid
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.025)';
    ctx.lineWidth = 1;
    for (let x = 0; x < W; x += 40) {
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, H);
      ctx.stroke();
    }
    for (let y = 0; y < H; y += 40) {
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(W, y);
      ctx.stroke();
    }

    // Draw Device Chassis Underlays (Glass Hardware silhouettes)
    drawChassisSilhouettes(ctx, kbX, kbY, kbW, kbH, mouseX, mouseY, fanCenters);

    const t = time / 1000;

    // Helper: Calculate RGB Color for any (x, y) coordinate
    function sampleColor(x, y) {
      let r = 0, g = 0, b = 0;

      if (currentEffect === 'cotton-candy') {
        // Deep purple base
        r = 50; g = 0; b = 80;
        blobs.forEach(blob => {
          const dx = x - blob.x;
          const dy = y - blob.y;
          const d2 = dx * dx + dy * dy;
          const r2 = blob.radius * blob.radius;
          if (d2 < r2) {
            const norm = 1 - d2 / r2;
            const alpha = norm * norm * 0.75;
            if (blob.isCyan) {
              r = r * (1 - alpha) + 0 * alpha;
              g = g * (1 - alpha) + 240 * alpha;
              b = b * (1 - alpha) + 255 * alpha;
            } else {
              r = r * (1 - alpha) + 255 * alpha;
              g = g * (1 - alpha) + 0 * alpha;
              b = b * (1 - alpha) + 180 * alpha;
            }
          }
        });
      } else if (currentEffect === 'rainbow') {
        const nx = x / W;
        const ny = y / H;
        const hue = (nx * 360 + ny * 180 + t * 90) % 360;
        const rgb = hsvToRgb(hue, 1, 1);
        r = rgb[0]; g = rgb[1]; b = rgb[2];
      } else if (currentEffect === 'biohazard') {
        const pulse = (Math.sin(t * 3.5 + (x + y) * 0.008) + 1) * 0.5;
        r = Math.floor(pulse * 30);
        g = Math.floor(180 + pulse * 75);
        b = Math.floor(40 + pulse * 50);
      } else if (currentEffect === 'cyberpunk') {
        const sweep = (x / W + t * 0.4) % 1.0;
        if (sweep < 0.5) {
          r = 0; g = 240; b = 255; // Neon Cyan
        } else {
          r = 255; g = 0; b = 150; // Hot Magenta
        }
      }

      // Cursor Light Ripple Overlay
      if (mousePos.active) {
        const mdx = x - mousePos.x;
        const mdy = y - mousePos.y;
        const dist = Math.sqrt(mdx * mdx + mdy * mdy);
        if (dist < 140) {
          const ripple = (1 - dist / 140) * 0.65;
          r = Math.min(255, r + 255 * ripple);
          g = Math.min(255, g + 255 * ripple);
          b = Math.min(255, b + 255 * ripple);
        }
      }

      return `rgb(${Math.round(r)}, ${Math.round(g)}, ${Math.round(b)})`;
    }

    // 1. Render Keyboard Keys
    keyboard.forEach(key => {
      const col = sampleColor(key.x, key.y);
      ctx.fillStyle = col;
      ctx.shadowColor = col;
      ctx.shadowBlur = 10;
      roundRect(ctx, key.x - key.w / 2, key.y - key.h / 2, key.w, key.h, key.rx);
      ctx.fill();
    });

    // 2. Render Mouse LEDs
    mouseLeds.forEach(m => {
      const col = sampleColor(m.x, m.y);
      ctx.fillStyle = col;
      ctx.shadowColor = col;
      ctx.shadowBlur = 14;
      ctx.beginPath();
      ctx.arc(m.x, m.y, m.size / 2, 0, Math.PI * 2);
      ctx.fill();
    });

    // 3. Render Radiator Fans
    fans.forEach(fan => {
      fan.leds.forEach(led => {
        const col = sampleColor(led.x, led.y);
        ctx.fillStyle = col;
        ctx.shadowColor = col;
        ctx.shadowBlur = 12;
        ctx.beginPath();
        ctx.arc(led.x, led.y, led.size / 2, 0, Math.PI * 2);
        ctx.fill();
      });
    });

    // 4. Render Ambient Desk LED Strip
    deskStrip.forEach(led => {
      const col = sampleColor(led.x, led.y);
      ctx.fillStyle = col;
      ctx.shadowColor = col;
      ctx.shadowBlur = 8;
      ctx.beginPath();
      ctx.arc(led.x, led.y, led.size / 2, 0, Math.PI * 2);
      ctx.fill();
    });

    // Reset shadow blur
    ctx.shadowBlur = 0;

    requestAnimationFrame(render);
  }

  requestAnimationFrame(render);
}

function drawChassisSilhouettes(ctx, kx, ky, kw, kh, mx, my, fans) {
  // Keyboard Frame
  ctx.fillStyle = 'rgba(18, 22, 34, 0.7)';
  ctx.strokeStyle = 'rgba(255, 255, 255, 0.08)';
  ctx.lineWidth = 1;
  roundRect(ctx, kx - 12, ky - 12, kw + 24, kh + 24, 10);
  ctx.fill();
  ctx.stroke();

  // Mouse Frame
  ctx.fillStyle = 'rgba(18, 22, 34, 0.7)';
  roundRect(ctx, mx - 36, my - 62, 72, 124, 24);
  ctx.fill();
  ctx.stroke();

  // Fan Frames
  fans.forEach(f => {
    ctx.fillStyle = 'rgba(18, 22, 34, 0.7)';
    ctx.beginPath();
    ctx.arc(f.x, f.y, 52, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    // Inner blade hub
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.04)';
    ctx.beginPath();
    ctx.arc(f.x, f.y, 22, 0, Math.PI * 2);
    ctx.stroke();
  });
}

function roundRect(ctx, x, y, width, height, radius) {
  ctx.beginPath();
  ctx.moveTo(x + radius, y);
  ctx.lineTo(x + width - radius, y);
  ctx.quadraticCurveTo(x + width, y, x + width, y + radius);
  ctx.lineTo(x + width, y + height - radius);
  ctx.quadraticCurveTo(x + width, y + height, x + width - radius, y + height);
  ctx.lineTo(x + radius, y + height);
  ctx.quadraticCurveTo(x, y + height, x, y + height - radius);
  ctx.lineTo(x, y + radius);
  ctx.quadraticCurveTo(x, y, x + radius, y);
  ctx.closePath();
}

function hsvToRgb(h, s, v) {
  let r, g, b;
  const i = Math.floor(h / 60) % 6;
  const f = h / 60 - Math.floor(h / 60);
  const p = v * (1 - s);
  const q = v * (1 - f * s);
  const t = v * (1 - (1 - f) * s);
  switch (i) {
    case 0: r = v; g = t; b = p; break;
    case 1: r = q; g = v; b = p; break;
    case 2: r = p; g = v; b = t; break;
    case 3: r = p; g = q; b = v; break;
    case 4: r = t; g = p; b = v; break;
    case 5: r = v; g = p; b = q; break;
  }
  return [Math.round(r * 255), Math.round(g * 255), Math.round(b * 255)];
}

/* ==========================================================================
   Liquid Glass Mouse Spotlight on Cards
   ========================================================================== */

function initGlassCardSpotlights() {
  const cards = document.querySelectorAll('.liquid-glass-card');
  cards.forEach(card => {
    card.addEventListener('mousemove', (e) => {
      const rect = card.getBoundingClientRect();
      const x = ((e.clientX - rect.left) / rect.width) * 100;
      const y = ((e.clientY - rect.top) / rect.height) * 100;
      card.style.setProperty('--mouse-x', `${x}%`);
      card.style.setProperty('--mouse-y', `${y}%`);
    });
  });
}

/* ==========================================================================
   Copy to Clipboard Functionality
   ========================================================================== */

function initCopyButtons() {
  const copyCodeBtn = document.getElementById('btn-copy-code');
  if (copyCodeBtn) {
    copyCodeBtn.addEventListener('click', () => {
      const code = `using System;
using XenRGB.SDK;

public sealed class MyRainbowWaveEffect : EffectBase
{
    public override RgbColor Evaluate(Point2D position, double time)
    {
        float speed = GetFloat("speed", 1.5f);
        float frequency = GetFloat("frequency", 8.0f);

        float phase = position.X * frequency + (float)time * speed;
        float hue = (phase * 60f) % 360f;
        if (hue < 0) hue += 360f;

        return RgbColor.FromHsv(hue, 1.0f, 1.0f);
    }
}`;
      navigator.clipboard.writeText(code).then(() => {
        const span = copyCodeBtn.querySelector('span');
        const orig = span.textContent;
        span.textContent = 'Copied!';
        setTimeout(() => { span.textContent = orig; }, 2000);
      });
    });
  }

  const copyCmdBtn = document.getElementById('btn-copy-cmd');
  if (copyCmdBtn) {
    copyCmdBtn.addEventListener('click', () => {
      const cmd = 'git clone https://github.com/Xenofff/XenRGB.git && dotnet build XenRGB.sln';
      navigator.clipboard.writeText(cmd).then(() => {
        copyCmdBtn.style.color = '#00f59b';
        setTimeout(() => { copyCmdBtn.style.color = ''; }, 2000);
      });
    });
  }
}

/* ==========================================================================
   Mobile Navigation Toggle
   ========================================================================== */

function initMobileNav() {
  const toggle = document.getElementById('menu-toggle');
  const menu = document.getElementById('nav-menu');
  if (!toggle || !menu) return;

  toggle.addEventListener('click', () => {
    menu.classList.toggle('open');
  });

  const links = menu.querySelectorAll('.nav-item');
  links.forEach(l => {
    l.addEventListener('click', () => {
      menu.classList.remove('open');
    });
  });
}
