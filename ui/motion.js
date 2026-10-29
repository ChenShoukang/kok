'use strict';
// Native window commands execute immediately. DWM owns minimize/close transitions;
// waiting for a renderer exit first delays input and races repeated window actions.
