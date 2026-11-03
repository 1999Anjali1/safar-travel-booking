import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastService } from './core/toast.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: `
    <router-outlet />
    <div class="toasts" aria-live="polite">
      @for (t of toasts.toasts(); track t.id) {
        <div class="toast" [class]="'toast toast--' + t.kind" role="status">
          <span>{{ t.text }}</span>
          <button type="button" class="toast__close" (click)="toasts.dismiss(t.id)" aria-label="Dismiss">×</button>
        </div>
      }
    </div>
  `,
})
export class App {
  protected readonly toasts = inject(ToastService);
}
