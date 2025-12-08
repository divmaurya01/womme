import { ComponentFixture, TestBed } from '@angular/core/testing';
import { JobPostedDetailComponent } from './job-posted-details';

describe('JobPostedDetailComponent', () => {
  let component: JobPostedDetailComponent;
  let fixture: ComponentFixture<JobPostedDetailComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [JobPostedDetailComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(JobPostedDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
