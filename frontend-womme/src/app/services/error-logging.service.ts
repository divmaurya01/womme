import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ErrorLoggingService {
  constructor() {
    console.log('🛠️ ErrorLoggingService initialized');
  }

  logError(error: {
    url: string;
    method: string;
    status: number;
    message: string;
    timestamp: string;
  }) {
    console.log('📨 Logging API error:', error);

    // You can send this to a backend later if needed.
    // Optional: Save to localStorage
    const logs = JSON.parse(localStorage.getItem('apiErrors') || '[]');
    logs.push(error);
    localStorage.setItem('apiErrors', JSON.stringify(logs));
  }
}
