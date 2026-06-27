import { gsap } from 'gsap';

/** True when the user has asked the OS to minimize motion. */
export function prefersReducedMotion(): boolean {
  return typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches;
}

/** Stagger-reveal a set of elements (e.g. coffee cards). No-ops under reduced motion. */
export function staggerReveal(targets: ArrayLike<Element>, stagger = 0.06): void {
  if (prefersReducedMotion() || targets.length === 0) return;
  gsap.from(Array.from(targets), {
    opacity: 0,
    y: 14,
    duration: 0.5,
    stagger,
    ease: 'power2.out',
    clearProps: 'all',
  });
}

/** Pop an element in (tag chips, toasts, badges). No-ops under reduced motion. */
export function popIn(target: Element, delay = 0): void {
  if (prefersReducedMotion()) return;
  gsap.from(target, {
    scale: 0.8,
    opacity: 0,
    duration: 0.3,
    delay,
    ease: 'back.out(1.7)',
    clearProps: 'all',
  });
}
