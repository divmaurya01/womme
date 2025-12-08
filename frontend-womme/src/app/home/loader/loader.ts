import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LoaderService } from '../../services/loader.service';

@Component({
  selector: 'app-loader',
  standalone: true,
  imports: [CommonModule],   
  templateUrl: './loader.html',
  styleUrls: ['./loader.scss']
})
export class LoaderComponent {
  isLoading = false;

  constructor(private loaderService: LoaderService) {
    this.loaderService.isLoading$.subscribe(status => {
      this.isLoading = status;
    });
  }
}
