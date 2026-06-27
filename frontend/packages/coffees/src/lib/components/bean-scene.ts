import { AfterViewInit, ChangeDetectionStrategy, Component, ElementRef, OnDestroy, viewChild } from '@angular/core';
import {
  AmbientLight,
  DirectionalLight,
  Group,
  Mesh,
  MeshStandardMaterial,
  PerspectiveCamera,
  Scene,
  SphereGeometry,
  WebGLRenderer,
} from 'three';

/**
 * The signature moment: a slowly tumbling cluster of warm coffee-bean shapes.
 * Loaded only via `@defer` so three.js stays out of the initial bundle.
 */
@Component({
  selector: 'ct-bean-scene',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<canvas #canvas class="block size-full"></canvas>`,
})
export class BeanScene implements AfterViewInit, OnDestroy {
  private readonly canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private renderer?: WebGLRenderer;
  private frame = 0;
  private cleanupResize?: () => void;

  ngAfterViewInit(): void {
    const canvas = this.canvasRef().nativeElement;
    const renderer = new WebGLRenderer({ canvas, antialias: true, alpha: true });
    this.renderer = renderer;

    const scene = new Scene();
    const camera = new PerspectiveCamera(45, 1, 0.1, 100);
    camera.position.set(0, 0, 6);

    scene.add(new AmbientLight(0xffffff, 0.6));
    const key = new DirectionalLight(0xffe9c8, 1.5);
    key.position.set(3, 4, 5);
    scene.add(key);

    const material = new MeshStandardMaterial({ color: 0x5a3a22, roughness: 0.55, metalness: 0.05 });
    const geometry = new SphereGeometry(0.5, 32, 24);
    const beans = new Group();
    for (let i = 0; i < 7; i++) {
      const bean = new Mesh(geometry, material);
      bean.scale.set(1, 0.62, 0.78); // flatten a sphere into a bean-ish ellipsoid
      bean.position.set((Math.random() - 0.5) * 3.6, (Math.random() - 0.5) * 2.4, (Math.random() - 0.5) * 1.5);
      bean.rotation.set(Math.random() * 3, Math.random() * 3, Math.random() * 3);
      beans.add(bean);
    }
    scene.add(beans);

    const resize = () => {
      const w = canvas.clientWidth || 1;
      const h = canvas.clientHeight || 1;
      renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
      renderer.setSize(w, h, false);
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
    };
    resize();
    window.addEventListener('resize', resize);
    this.cleanupResize = () => window.removeEventListener('resize', resize);

    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const render = () => {
      beans.rotation.y += 0.0035;
      beans.rotation.x += 0.0012;
      renderer.render(scene, camera);
      if (!reduce) this.frame = requestAnimationFrame(render);
    };
    render();
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this.frame);
    this.cleanupResize?.();
    this.renderer?.dispose();
  }
}
