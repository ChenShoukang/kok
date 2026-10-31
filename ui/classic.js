'use strict';
// Old skin preferences remain readable; every window uses the flat interface.

Kok.extend('core','classic.js:applyThemeBeforeSkin','applyTheme',applyThemeBeforeSkin=>function(){applyThemeBeforeSkin();document.body.dataset.skin='flat';});

if(document.body.dataset.theme)applyTheme();
