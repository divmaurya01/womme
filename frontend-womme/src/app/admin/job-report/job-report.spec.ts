import { ComponentFixture, TestBed } from '@angular/core/testing';

import { JobReport } from './job-report';

describe('JobReport', () => {
  let component: JobReport;
  let fixture: ComponentFixture<JobReport>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [JobReport]
    })
    .compileComponents();

    fixture = TestBed.createComponent(JobReport);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
