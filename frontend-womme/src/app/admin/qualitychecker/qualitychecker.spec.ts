import { ComponentFixture, TestBed } from '@angular/core/testing';

import { QualityChecker } from './qualitychecker';

describe('PostedJobTransaction', () => {
  let component: QualityChecker;
  let fixture: ComponentFixture<QualityChecker>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QualityChecker]
    })
    .compileComponents();

    fixture = TestBed.createComponent(QualityChecker);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
