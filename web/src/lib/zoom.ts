// layout.css zooms the whole UI on big screens. getBoundingClientRect() and pointer coordinates
// are window pixels, while a length set in style is multiplied by the zoom: divide by this first.
export function uiZoom(): number {
	if (typeof document === 'undefined') return 1;
	const zoom = parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--ui-zoom'));
	return zoom > 0 ? zoom : 1;
}
