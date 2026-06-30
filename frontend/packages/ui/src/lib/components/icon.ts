import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, input } from '@angular/core';
import {
  ArrowLeft,
  ArrowRight,
  Camera,
  Check,
  ChevronDown,
  Coffee,
  type IconNode,
  LogOut,
  Pencil,
  Plus,
  Search,
  Star,
  Sun,
  Moon,
  Trash2,
  Upload,
  User,
  X,
  createElement,
} from 'lucide';

/** Curated icon set (kebab name → lucide node). Add as needed. */
const ICONS: Record<string, IconNode> = {
  coffee: Coffee,
  search: Search,
  camera: Camera,
  plus: Plus,
  star: Star,
  sun: Sun,
  moon: Moon,
  check: Check,
  x: X,
  pencil: Pencil,
  trash: Trash2,
  upload: Upload,
  user: User,
  logout: LogOut,
  back: ArrowLeft,
  'arrow-right': ArrowRight,
  'chevron-down': ChevronDown,
};

export type IconName = keyof typeof ICONS;

/** Renders a lucide icon as inline SVG (framework-agnostic core, no Angular peer). */
@Component({
  selector: 'ct-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex' },
  template: '',
})
export class Icon {
  readonly name = input.required<IconName>();
  readonly size = input(20);
  readonly strokeWidth = input(2);

  private readonly host = inject(ElementRef<HTMLElement>);

  constructor() {
    effect(() => {
      const node = ICONS[this.name()];
      const el = this.host.nativeElement;
      el.replaceChildren();
      if (!node) return;
      const svg = createElement(node);
      svg.setAttribute('width', String(this.size()));
      svg.setAttribute('height', String(this.size()));
      svg.setAttribute('stroke-width', String(this.strokeWidth()));
      el.appendChild(svg);
    });
  }
}
