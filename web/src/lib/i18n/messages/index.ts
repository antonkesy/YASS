import { common } from './common';
import { dialogs } from './dialogs';
import { home } from './home';
import { review } from './review';
import { settings } from './settings';

/** Each message carries all four languages, so a missing translation is a type error. */
export const messages = { common, home, review, settings, dialogs };
