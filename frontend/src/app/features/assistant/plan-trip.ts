import { DatePipe } from '@angular/common';
import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AssistantApi, firstError } from '../../core/api';
import { TripPlan } from '../../core/models';
import { Cover, QarPipe } from '../../shared/ui';

interface Turn {
  question: string;
  plan: TripPlan | null;
  error: string | null;
}

@Component({
  selector: 'app-plan-trip',
  imports: [FormsModule, RouterLink, DatePipe, QarPipe, Cover],
  templateUrl: './plan-trip.html',
})
export class PlanTrip {
  private readonly api = inject(AssistantApi);
  private readonly input = viewChild<ElementRef<HTMLTextAreaElement>>('box');

  protected message = '';
  protected readonly turns = signal<Turn[]>([]);
  protected readonly thinking = signal(false);

  protected readonly examples = [
    'A long weekend somewhere cool in December, under QAR 3,500 each',
    'Honeymoon by the beach for the two of us, budget 16k total',
    'Something fun with the kids this weekend in Qatar',
    'History and culture, 5 days, January',
  ];

  ask(text = this.message): void {
    const question = text.trim();
    if (question.length < 3 || this.thinking()) return;

    this.message = '';
    this.thinking.set(true);
    this.turns.update((t) => [...t, { question, plan: null, error: null }]);
    const index = this.turns().length - 1;

    this.api.plan(question).subscribe({
      next: (plan) => this.finish(index, { plan }),
      error: (e) => this.finish(index, { error: firstError(e) }),
    });
  }

  onKey(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.ask();
    }
  }

  private finish(index: number, update: Partial<Turn>): void {
    this.turns.update((t) => t.map((turn, i) => (i === index ? { ...turn, ...update } : turn)));
    this.thinking.set(false);
    setTimeout(() => this.input()?.nativeElement.focus());
  }
}
