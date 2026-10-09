/**
 * The icon set from "Scanner Stage v2.dc.html", transcribed. All 24×24, stroked with
 * `currentColor`, round caps and joins — the mockup's own `sv()` helper in reverse.
 *
 * Shapes stay as `rect` / `circle` where the mockup used them rather than being
 * flattened to paths, so a change can be diffed against the mockup line for line.
 */

export type Shape =
	| { readonly kind: 'path'; readonly d: string }
	| {
			readonly kind: 'rect';
			readonly x: number;
			readonly y: number;
			readonly w: number;
			readonly h: number;
			readonly rx: number;
	  }
	| { readonly kind: 'circle'; readonly cx: number; readonly cy: number; readonly r: number };

const p = (d: string): Shape => ({ kind: 'path', d });
const rect = (x: number, y: number, w: number, h: number, rx: number): Shape => ({
	kind: 'rect',
	x,
	y,
	w,
	h,
	rx
});
const circle = (cx: number, cy: number, r: number): Shape => ({ kind: 'circle', cx, cy, r });

/** Size and stroke the mockup draws each icon at, where it differs from 18px / 1.75. */
export type IconSpec = {
	readonly shapes: readonly Shape[];
	readonly size?: number;
	readonly stroke?: number;
};

export const DEFAULT_SIZE = 18;
export const DEFAULT_STROKE = 1.75;

export const icons = {
	rotateLeft: {
		shapes: [p('M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8'), p('M3 3v5h5')]
	},
	rotateRight: {
		shapes: [p('M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8'), p('M21 3v5h-5')]
	},
	trash: {
		shapes: [
			p('M3 6h18'),
			p('M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6'),
			p('M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2')
		]
	},
	crop: {
		shapes: [p('M6 2v14a2 2 0 0 0 2 2h14'), p('M18 22V8a2 2 0 0 0-2-2H2')]
	},
	rescan: {
		shapes: [
			p('M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8'),
			p('M21 3v5h-5'),
			p('M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16'),
			p('M8 16H3v5')
		]
	},
	wand: {
		shapes: [
			p(
				'm21.64 3.64-1.28-1.28a1.21 1.21 0 0 0-1.72 0L2.36 18.64a1.21 1.21 0 0 0 0 1.72l1.28 1.28a1.2 1.2 0 0 0 1.72 0L21.64 5.36a1.2 1.2 0 0 0 0-1.72'
			),
			p('m14 7 3 3'),
			p('M5 6v4'),
			p('M19 14v4'),
			p('M10 2v2'),
			p('M7 8H3'),
			p('M21 16h-4'),
			p('M11 3H9')
		]
	},
	scissors: {
		shapes: [
			circle(6, 6, 3),
			circle(6, 18, 3),
			p('M20 4 8.12 15.88'),
			p('M14.47 14.48 20 20'),
			p('M8.12 8.12 12 12')
		]
	},
	layers: {
		shapes: [
			p(
				'm12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83Z'
			),
			p('m22 17.65-9.17 4.16a2 2 0 0 1-1.66 0L2 17.65'),
			p('m22 12.65-9.17 4.16a2 2 0 0 1-1.66 0L2 12.65')
		]
	},
	download: {
		shapes: [p('M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'), p('m7 10 5 5 5-5'), p('M12 15V3')]
	},
	archive: {
		shapes: [rect(2, 3, 20, 5, 1), p('M4 8v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8'), p('M10 12h4')]
	},
	back: { shapes: [p('m12 19-7-7 7-7'), p('M19 12H5')] },
	grid: {
		shapes: [
			rect(3, 3, 7, 7, 1.5),
			rect(14, 3, 7, 7, 1.5),
			rect(14, 14, 7, 7, 1.5),
			rect(3, 14, 7, 7, 1.5)
		]
	},
	list: {
		shapes: [
			p('M8 6h13'),
			p('M8 12h13'),
			p('M8 18h13'),
			p('M3 6h.01'),
			p('M3 12h.01'),
			p('M3 18h.01')
		]
	},
	plus: { shapes: [p('M5 12h14'), p('M12 5v14')] },
	close: { shapes: [p('M18 6 6 18'), p('m6 6 12 12')] },
	/** The mockup's 12px/2.5 close, used inside pills and chips. */
	closeSmall: { shapes: [p('M18 6 6 18'), p('m6 6 12 12')], size: 12, stroke: 2.5 },
	chevronLeft: { shapes: [p('m15 18-6-6 6-6')] },
	chevronRight: { shapes: [p('m9 18 6-6-6-6')] },
	scan: {
		shapes: [
			p('M3 7V5a2 2 0 0 1 2-2h2'),
			p('M17 3h2a2 2 0 0 1 2 2v2'),
			p('M21 17v2a2 2 0 0 1-2 2h-2'),
			p('M7 21H5a2 2 0 0 1-2-2v-2'),
			p('M7 12h10')
		],
		size: 24,
		stroke: 2
	},
	check: { shapes: [p('M20 6 9 17l-5-5')], size: 16, stroke: 2.5 },
	/** Same glyph as `check`, drawn smaller for the autosave strip. */
	checkSmall: { shapes: [p('M20 6 9 17l-5-5')], size: 14, stroke: 2.5 },
	sliders: {
		shapes: [
			p('M21 4h-7'),
			p('M10 4H3'),
			p('M21 12h-9'),
			p('M8 12H3'),
			p('M21 20h-5'),
			p('M12 20H3'),
			p('M14 2v4'),
			p('M8 10v4'),
			p('M16 18v4')
		]
	},
	folder: {
		shapes: [
			p(
				'M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z'
			)
		],
		size: 16
	},
	folderOpen: {
		shapes: [
			p(
				'm6 14 1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.54 6a2 2 0 0 1-1.95 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2'
			)
		]
	},
	exportTo: {
		shapes: [
			p('M12 3v12'),
			p('m8 11 4 4 4-4'),
			p('M8 5H6a2 2 0 0 0-2 2v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2')
		]
	},
	pencil: {
		shapes: [
			p(
				'M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z'
			),
			p('m15 5 4 4')
		],
		size: 15
	},
	template: {
		shapes: [
			p('M8 3H7a2 2 0 0 0-2 2v5a2 2 0 0 1-2 2 2 2 0 0 1 2 2v5c0 1.1.9 2 2 2h1'),
			p('M16 21h1a2 2 0 0 0 2-2v-5c0-1.1.9-2 2-2a2 2 0 0 1-2-2V5a2 2 0 0 0-2-2h-1')
		],
		size: 16
	},
	search: { shapes: [circle(11, 11, 8), p('m21 21-4.3-4.3')], size: 16 },
	/** The header's language menu. */
	globe: {
		shapes: [
			circle(12, 12, 9),
			p('M3 12h18'),
			p('M12 3a14 14 0 0 1 0 18'),
			p('M12 3a14 14 0 0 0 0 18')
		],
		size: 16
	},
	/** Not in the mockup either: keyboard shortcuts and issue reporting. */
	keyboard: {
		shapes: [
			rect(2, 6, 20, 12, 2),
			p('M6 10h.01'),
			p('M10 10h.01'),
			p('M14 10h.01'),
			p('M18 10h.01'),
			p('M7 14h10')
		],
		size: 16
	},
	bug: {
		shapes: [
			p('M8 6V4.5a4 4 0 0 1 8 0V6'),
			rect(7, 6, 10, 13, 5),
			p('M12 10v9'),
			p('M3 13h4'),
			p('M17 13h4'),
			p('M4 7l3 2'),
			p('M20 7l-3 2'),
			p('M4 19l3-2'),
			p('M20 19l-3-2')
		],
		size: 16
	},
	/** Not in the mockup, which has no full-screen page view; this app does. */
	expand: { shapes: [p('M15 3h6v6'), p('M9 21H3v-6'), p('M21 3l-7 7'), p('M3 21l7-7')] },
	/** Nor a full-screen overview, which zooms every thumbnail at once. */
	zoomIn: {
		shapes: [circle(11, 11, 7), p('m20 20-4.5-4.5'), p('M8 11h6'), p('M11 8v6')],
		size: 16
	},
	zoomOut: { shapes: [circle(11, 11, 7), p('m20 20-4.5-4.5'), p('M8 11h6')], size: 16 },
	sort: {
		shapes: [p('m3 16 4 4 4-4'), p('M7 20V4'), p('m21 8-4-4-4 4'), p('M17 4v16')],
		size: 15
	},

	// Preset icons. The mockup draws these at 15px in the picker and the preset pills.
	file: {
		shapes: [
			p('M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z'),
			p('M14 2v4a2 2 0 0 0 2 2h4'),
			p('M10 13H8'),
			p('M16 17H8'),
			p('M16 13h-2')
		],
		size: 15
	},
	copy: {
		shapes: [rect(8, 8, 14, 14, 2), p('M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2')],
		size: 15
	},
	receipt: {
		shapes: [
			p('M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1 2-1 2 1V2l-2 1-2-1-2 1-2-1-2 1-2-1-2 1Z'),
			p('M16 8h-6a2 2 0 1 0 0 4h4a2 2 0 1 1 0 4H8'),
			p('M12 17.5v-11')
		],
		size: 15
	},
	image: {
		shapes: [rect(3, 3, 18, 18, 2), circle(9, 9, 2), p('m21 15-3.09-3.09a2 2 0 0 0-2.82 0L6 21')],
		size: 15
	},
	briefcase: {
		shapes: [p('M16 20V4a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16'), rect(2, 6, 20, 14, 2)],
		size: 15
	},
	heart: {
		shapes: [
			p(
				'M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z'
			)
		],
		size: 15
	},
	home: {
		shapes: [
			p('M15 21v-8a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v8'),
			p(
				'M3 10a2 2 0 0 1 .709-1.528l7-5.999a2 2 0 0 1 2.582 0l7 5.999A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z'
			)
		],
		size: 15
	},
	book: {
		shapes: [
			p('M4 19.5v-15A2.5 2.5 0 0 1 6.5 2H19a1 1 0 0 1 1 1v18a1 1 0 0 1-1 1H6.5a1 1 0 0 1 0-5H20')
		],
		size: 15
	},
	mail: {
		shapes: [rect(2, 4, 20, 16, 2), p('m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7')],
		size: 15
	},
	tag: {
		shapes: [
			p(
				'M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z'
			),
			circle(7.5, 7.5, 0.5)
		],
		size: 15
	},
	zap: {
		shapes: [
			p(
				'M4 14a1 1 0 0 1-.78-1.63l9.9-10.2a.5.5 0 0 1 .86.46l-1.92 6.02A1 1 0 0 0 13 10h7a1 1 0 0 1 .78 1.63l-9.9 10.2a.5.5 0 0 1-.86-.46l1.92-6.02A1 1 0 0 0 11 14z'
			)
		],
		size: 15
	},
	star: {
		shapes: [
			p(
				'M11.5 2.3a.53.53 0 0 1 .95 0l2.31 4.68a2.12 2.12 0 0 0 1.6 1.16l5.16.76a.53.53 0 0 1 .3.9l-3.74 3.64a2.12 2.12 0 0 0-.61 1.88l.88 5.14a.53.53 0 0 1-.77.56l-4.62-2.43a2.12 2.12 0 0 0-1.97 0L6.4 21.01a.53.53 0 0 1-.77-.56l.88-5.14a2.12 2.12 0 0 0-.61-1.88L2.16 9.8a.53.53 0 0 1 .3-.9l5.16-.76a2.12 2.12 0 0 0 1.6-1.16Z'
			)
		],
		size: 15
	},
	sun: {
		shapes: [
			circle(12, 12, 4),
			p('M12 2v2'),
			p('M12 20v2'),
			p('m4.93 4.93 1.41 1.41'),
			p('m17.66 17.66 1.41 1.41'),
			p('M2 12h2'),
			p('M20 12h2'),
			p('m6.34 17.66-1.41 1.41'),
			p('m19.07 4.93-1.41 1.41')
		],
		size: 16
	},
	/** ⋯: the menu a phone's header folds its other buttons into. */
	more: { shapes: [circle(5, 12, 1), circle(12, 12, 1), circle(19, 12, 1)], size: 18, stroke: 2.5 },
	moon: { shapes: [p('M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z')], size: 16 },
	/** Light or dark: a circle, one half of it marked. */
	contrast: { shapes: [circle(12, 12, 10), p('M12 18a6 6 0 0 0 0-12v12z')] }
} as const satisfies Record<string, IconSpec>;

export type IconName = keyof typeof icons;

/** The twelve glyphs the settings sheet offers when naming a preset. */
export const presetIcons = [
	'star',
	'file',
	'copy',
	'receipt',
	'image',
	'briefcase',
	'heart',
	'home',
	'book',
	'mail',
	'tag',
	'zap'
] as const satisfies readonly IconName[];

export type PresetIconName = (typeof presetIcons)[number];
