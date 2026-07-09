import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  input,
  viewChild,
} from '@angular/core';
import { prefersReducedMotion } from '@coffee-tracker/util';

interface Bean {
  x: number;
  y: number;
  r: number;
  a: number;
  va: number;
  vy: number;
  drift: number;
  o: number;
}
interface Wisp {
  x: number;
  phase: number;
  speed: number;
  amp: number;
}

/**
 * The signature ambient moment: faint coffee beans drifting slowly upward through a
 * warm glow, with a few rising steam wisps. Pure 2D canvas (no WebGL) — cheap enough
 * to run on first paint. Theme-aware (beans lift to tan on dark) and reduced-motion
 * aware (renders a single static frame). Set `beanCount` to 0 for a steam-only label.
 */
@Component({
  selector: 'ct-bean-scene',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<canvas #canvas class="block size-full"></canvas>`,
})
export class BeanScene implements AfterViewInit, OnDestroy {
  readonly beanCount = input(10);
  readonly wispCount = input(6);
  readonly glow = input('rgba(204,139,67,.20)');

  private readonly canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private raf = 0;
  private ro?: ResizeObserver;

  ngAfterViewInit(): void {
    const canvas = this.canvasRef().nativeElement;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const reduce = prefersReducedMotion();
    let w = 0;
    let h = 0;
    let beans: Bean[] = [];
    let wisps: Wisp[] = [];

    // Tan + brighter on dark so the beans stay visible against the espresso ground.
    const palette = () =>
      document.documentElement.dataset['theme'] === 'dark'
        ? { fill: '231,191,138', crease: '158,112,64', scale: 3.0 }
        : { fill: '74,46,26', crease: '38,22,12', scale: 1.0 };

    const size = () => {
      const dpr = Math.min(2, window.devicePixelRatio || 1);
      w = canvas.clientWidth;
      h = canvas.clientHeight;
      canvas.width = Math.max(1, Math.round(w * dpr));
      canvas.height = Math.max(1, Math.round(h * dpr));
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    };

    const init = () => {
      size();
      beans = Array.from({ length: this.beanCount() }, () => ({
        x: Math.random() * w,
        y: Math.random() * h,
        r: 9 + Math.random() * 16,
        a: Math.random() * Math.PI,
        va: (Math.random() - 0.5) * 0.004,
        vy: -0.06 - Math.random() * 0.12,
        drift: (Math.random() - 0.5) * 0.25,
        o: 0.05 + Math.random() * 0.08,
      }));
      wisps = Array.from({ length: this.wispCount() }, () => ({
        x: w * (0.2 + 0.6 * Math.random()),
        phase: Math.random() * 6.28,
        speed: 0.2 + Math.random() * 0.3,
        amp: 14 + Math.random() * 22,
      }));
    };

    const bg = () => {
      const g = ctx.createRadialGradient(w * 0.28, h * 0.15, 0, w * 0.28, h * 0.15, Math.max(w, h));
      g.addColorStop(0, this.glow());
      g.addColorStop(0.5, 'rgba(243,233,219,0)');
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, w, h);
    };

    const drawBean = (b: Bean) => {
      const tp = palette();
      const o = b.o * tp.scale;
      ctx.save();
      ctx.translate(b.x, b.y);
      ctx.rotate(b.a);
      // body — an elongated oval
      ctx.fillStyle = `rgba(${tp.fill},${o})`;
      ctx.beginPath();
      ctx.ellipse(0, 0, b.r, b.r * 0.58, 0, 0, 6.28);
      ctx.fill();
      // rim
      ctx.lineWidth = Math.max(1, b.r * 0.06);
      ctx.strokeStyle = `rgba(${tp.crease},${o * 0.8})`;
      ctx.beginPath();
      ctx.ellipse(0, 0, b.r, b.r * 0.58, 0, 0, 6.28);
      ctx.stroke();
      // center fold — the S-curved groove that reads as "coffee bean"
      ctx.lineWidth = Math.max(1.2, b.r * 0.13);
      ctx.lineCap = 'round';
      ctx.strokeStyle = `rgba(${tp.crease},${o * 1.7})`;
      ctx.beginPath();
      ctx.moveTo(-b.r * 0.7, 0);
      ctx.bezierCurveTo(-b.r * 0.24, -b.r * 0.3, b.r * 0.24, b.r * 0.3, b.r * 0.7, 0);
      ctx.stroke();
      ctx.restore();
    };

    /** One motionless frame — the reduced-motion rendering (no rAF loop). */
    const drawStatic = () => {
      ctx.clearRect(0, 0, w, h);
      bg();
      beans.forEach(drawBean);
    };

    let t = 0;
    const frame = () => {
      t += 0.016;
      ctx.clearRect(0, 0, w, h);
      bg();
      // steam wisps — faint sinuous vertical strokes
      for (const p of wisps) {
        ctx.beginPath();
        for (let y = h; y > h * 0.15; y -= 6) {
          const prog = (h - y) / h;
          const x = p.x + Math.sin(y * 0.02 + p.phase + t * p.speed) * p.amp * prog;
          if (y === h) ctx.moveTo(x, y);
          else ctx.lineTo(x, y);
        }
        ctx.strokeStyle = 'rgba(255,253,246,.05)';
        ctx.lineWidth = 16;
        ctx.lineCap = 'round';
        ctx.stroke();
      }
      // beans drift up and respawn at the bottom
      for (const b of beans) {
        b.y += b.vy;
        b.x += b.drift;
        b.a += b.va;
        if (b.y < -30) {
          b.y = h + 30;
          b.x = Math.random() * w;
        }
        drawBean(b);
      }
      this.raf = requestAnimationFrame(frame);
    };

    // ResizeObserver so the first real layout (deferred / absolutely-positioned host
    // can be 0 at AfterViewInit) and any later resize reseed at the right size.
    // Under reduced motion there is no frame loop to repaint after the resize wipes
    // the canvas, so redraw the static frame here too.
    this.ro = new ResizeObserver(() => {
      init();
      if (reduce) drawStatic();
    });
    this.ro.observe(canvas);
    init();

    if (reduce) {
      drawStatic();
      return;
    }
    frame();
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this.raf);
    this.ro?.disconnect();
  }
}
