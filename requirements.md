# New Version of FlintSoft.Components

I want a component library for blazor which can be used in a blazor page and in forms on a blazor page.

The Lib should contain:
* Input Controls
* Buttons
* Modals
* Toast
* Tables

Examples for the inputs in plain html and css are in ideas/index.html

It is important that the lib is responsive and can be used on desktop and mobile.

## Input Controls

The input control is hosted inside a div which will combine input and label for that input (see Basic input). 

Radio buttons and groups should be seperate in look
Checkboxes should be seperate

### Open Questions

Can a single input be used to handle text, int and decimal, date, time inputs?

## Buttons

Buttons will have the following appearances:
* Primary
* Secondary
* Action
* Danger/Failure
* Success

## Modals

Modals should be a overlay which will darken the background and prohibit any events in the background. the content should be dynamic (ChildContent).
Each modal may has a title (property) and a x as a close button. Close events will be invoked. each modal has a open method (this can be called when the modal component is uses and has a ref on it). Open is used to initalized the modal (set data to model or load data).

## Toast

A toast has the following styles
* Info
* Success
* Danger
* Warning

a toast has a title (if set) and a content. the content can be text or childcontent

## Table

A table has a header and content. the header and content has columns. the table should be repsonsive so it can be used in desktop and mobile

### Questions

Maybe use divs instead of html table?

## Demo

In the Blazor demo project, there should be a Page for each control section to show how it works