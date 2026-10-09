import { common } from './common';
import { dialogs } from './dialogs';
import { home } from './home';
import { review } from './review';
import { settings } from './settings';

/**
 * Every message the UI shows, by area. An area is one screen or group of components; its file
 * holds each message in all four languages side by side, so a missing translation is a type
 * error rather than an English word in a German page.
 */
export const messages = { common, home, review, settings, dialogs };
