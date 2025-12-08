import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Rolemaster } from './rolemaster';

describe('Rolemaster', () => {
  let component: Rolemaster;
  let fixture: ComponentFixture<Rolemaster>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Rolemaster]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Rolemaster);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
