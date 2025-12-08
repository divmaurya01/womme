import {
  HttpEvent,
  HttpInterceptor,
  HttpHandler,
  HttpRequest,
  HttpErrorResponse
} from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import Swal from 'sweetalert2';
import { ErrorLoggingService } from './error-logging.service';

@Injectable()
export class ErrorLoggerInterceptor implements HttpInterceptor {
  constructor(private logger: ErrorLoggingService) {
    console.log('✅ ErrorLoggerInterceptor initialized');
  }

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return next.handle(req).pipe(
      catchError((error: HttpErrorResponse) => {
        console.error('🚨 Interceptor caught error:', error);

        const errorDetails = {
          url: req.url,
          method: req.method,
          status: error.status,
          message: error.message,
          timestamp: new Date().toISOString()
        };

        // Log the error using service
        this.logger.logError(errorDetails);

        // Show SweetAlert
        // Swal.fire({
        //   icon: 'error',
        //   title: 'API Failed',
        //   text: 'Something went wrong while fetching data.',
        //   confirmButtonColor: '#d33'
        // });

        return throwError(() => error);
      })
    );
  }
}
