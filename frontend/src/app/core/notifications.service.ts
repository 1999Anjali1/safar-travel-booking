import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';
import { BookingEvent } from './models';
import { ToastService } from './toast.service';

/** Live booking events for the staff portal, pushed by the API over SignalR (WebSockets). */
@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly auth = inject(AuthService);
  private readonly toasts = inject(ToastService);
  private connection: HubConnection | null = null;

  /** Latest events, newest first. */
  readonly events = signal<BookingEvent[]>([]);
  readonly connected = signal(false);

  async connect(): Promise<void> {
    if (this.connection) return;

    this.connection = new HubConnectionBuilder()
      .withUrl(environment.hubUrl, { accessTokenFactory: () => this.auth.accessToken() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('bookingEvent', (event: BookingEvent) => {
      this.events.update((list) => [event, ...list].slice(0, 20));
      const verb = event.event === 'BookingConfirmed' ? 'New booking' : 'Booking cancelled';
      this.toasts.show(`${verb}: ${event.customerName} - ${event.packageTitle} (${event.seats} seat${event.seats > 1 ? 's' : ''})`,
        event.event === 'BookingConfirmed' ? 'success' : 'info');
    });
    this.connection.onreconnected(() => this.connected.set(true));
    this.connection.onreconnecting(() => this.connected.set(false));
    this.connection.onclose(() => this.connected.set(false));

    try {
      await this.connection.start();
      this.connected.set(true);
    } catch {
      this.connected.set(false);
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection?.state !== HubConnectionState.Disconnected) await this.connection?.stop();
    this.connection = null;
    this.connected.set(false);
  }
}
