import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { NgCircleProgressModule } from 'ng-circle-progress';
// import { provideAnimations } from '@angular/platform-browser/animations';
// import { provideHttpClient } from '@angular/common/http';
import { HttpClient } from '@angular/common/http';
import { LoaderComponent } from './home/loader/loader';


@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterModule, CommonModule,MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatInputModule,
    MatFormFieldModule,
    NgCircleProgressModule,
    LoaderComponent,
    
],
  templateUrl: './app.html',
  styleUrls: ['./app.scss'],
  // providers: [provideNoopAnimations()]

})
export class AppComponent {}
