import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Issuetransaction } from './issuetransaction';

describe('Issuetransaction', () => {
  let component: Issuetransaction;
  let fixture: ComponentFixture<Issuetransaction>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Issuetransaction]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Issuetransaction);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
