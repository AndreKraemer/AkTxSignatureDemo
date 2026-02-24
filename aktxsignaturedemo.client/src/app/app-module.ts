import { HttpClientModule } from '@angular/common/http';
import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';

import { DocumentEditorModule } from '@txtextcontrol/tx-ng-document-editor';
import { DocumentViewerModule } from '@txtextcontrol/tx-ng-document-viewer';

import { AppRoutingModule } from './app-routing-module';
import { App } from './app';
import { HomeComponent } from './pages/home/home';
import { EditorComponent } from './pages/editor/editor';
import { ViewerComponent } from './pages/viewer/viewer';
import { SignComponent } from './pages/sign/sign';

@NgModule({
  declarations: [App, HomeComponent, EditorComponent, ViewerComponent, SignComponent],
  imports: [
    BrowserModule,
    HttpClientModule,
    AppRoutingModule,
    DocumentEditorModule,
    DocumentViewerModule,
  ],
  providers: [],
  bootstrap: [App],
})
export class AppModule {}
