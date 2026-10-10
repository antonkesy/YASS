export function moveIndex(count: number, from: number, to: number): number[] {
	const order = Array.from({ length: count }, (_, i) => i);
	if (from === to || from < 0 || to < 0 || from >= count || to >= count) return order;
	const [moved] = order.splice(from, 1);
	order.splice(to, 0, moved);
	return order;
}

export function isIdentity(order: number[]): boolean {
	return order.every((v, i) => v === i);
}

/** Points in a gap or past the last thumb snap to the nearest one. */
export function slotAt(rects: DOMRectReadOnly[], x: number, y: number): number | null {
	let nearest: number | null = null;
	let best = Infinity;
	for (let i = 0; i < rects.length; i++) {
		const r = rects[i];
		if (x >= r.left && x <= r.right && y >= r.top && y <= r.bottom) return i;
		const dx = x - (r.left + r.right) / 2;
		const dy = y - (r.top + r.bottom) / 2;
		const distance = dx * dx + dy * dy;
		if (distance < best) {
			best = distance;
			nearest = i;
		}
	}
	return nearest;
}

export const EDGE_ZONE = 72;
export const EDGE_SPEED = 18;

export function edgeScroll(
	rect: { left: number; right: number; top: number; bottom: number },
	x: number,
	y: number
): { dx: number; dy: number } {
	const pull = (near: number, far: number, at: number) => {
		if (far - near <= 0) return 0;
		if (at < near + EDGE_ZONE)
			return -Math.min(1, (near + EDGE_ZONE - at) / EDGE_ZONE) * EDGE_SPEED;
		if (at > far - EDGE_ZONE) return Math.min(1, (at - (far - EDGE_ZONE)) / EDGE_ZONE) * EDGE_SPEED;
		return 0;
	};
	return { dx: pull(rect.left, rect.right, x), dy: pull(rect.top, rect.bottom, y) };
}
