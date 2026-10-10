import { reduced } from '$lib/motion';

/**
 * Smooths the jumps a container's children make when something in it changes size, appears or
 * goes: a section opening, a button that only shows for some pages, a warning coming up. Use it
 * as an attachment, `{@attach autoAnimate}`, on a container whose direct children move about.
 *
 * - A child that moves — because a sibling grew, shrank, came or went — glides from where it
 *   was to where it now is (FLIP: measured before, played back as a transform).
 * - A child that arrives fades and scales in.
 * - A child that leaves is replaced for a moment by a copy fading out where it stood.
 *
 * Only jumps are animated. A change that already happens gradually — a section sliding open
 * with a Svelte transition, or a child easing its own height — is left alone, so the two never
 * fight. Nothing plays for anyone who asked for less motion.
 */

type Rect = { top: number; left: number; width: number; height: number };

const DURATION = 240;
const EASING = 'cubic-bezier(0.2, 0.8, 0.2, 1)';
/** Smaller moves than this in one go are a transition already under way, not a jump. */
const JUMP = 6;
const GHOST = 'aaGhost';

/** Children that are not part of the flow: popups, overlays, our own ghosts. */
function skip(child: Element): boolean {
	if (!(child instanceof HTMLElement) || child.dataset[GHOST] !== undefined) return true;
	const position = getComputedStyle(child).position;
	return position === 'absolute' || position === 'fixed';
}

/**
 * A child already easing its own height — a CSS transition, as the home screen's settings chips
 * do — moves its siblings smoothly by itself; adding a glide on top would make them overshoot.
 */
function easingHeight(child: Element): boolean {
	if (typeof CSSTransition === 'undefined') return false;
	return child
		.getAnimations()
		.some(
			(a) =>
				a instanceof CSSTransition && a.transitionProperty === 'height' && a.playState === 'running'
		);
}

export function autoAnimate(container: HTMLElement): () => void {
	if (typeof ResizeObserver === 'undefined' || typeof MutationObserver === 'undefined')
		return () => {};

	const rects = new Map<Element, Rect>();
	// Unknown until the first measure, which the resize observer's first report makes: measuring
	// here, while the page is still being put together, would lay the whole page out early.
	let width = -1;

	function measure(child: Element, origin: DOMRect): Rect {
		const r = child.getBoundingClientRect();
		return {
			top: r.top - origin.top,
			left: r.left - origin.left,
			width: r.width,
			height: r.height
		};
	}

	function snapshot(origin = container.getBoundingClientRect()) {
		width = origin.width;
		rects.clear();
		for (const child of container.children)
			if (!skip(child)) rects.set(child, measure(child, origin));
	}

	/** Plays back every child's jump from its last measured place to where it is now. */
	function settle(added: ReadonlySet<Element> = new Set()) {
		const origin = container.getBoundingClientRect();
		// The container itself changed width — the window was resized — and everything reflowed
		// with it: that is not something to animate.
		if (reduced() || Math.abs(origin.width - width) > 1) return snapshot(origin);
		const moves: { child: Element; dx: number; dy: number }[] = [];
		const entering: Element[] = [];
		for (const child of container.children) {
			if (skip(child)) continue;
			const after = measure(child, origin);
			const before = rects.get(child);
			rects.set(child, after);
			if (added.has(child)) entering.push(child);
			if (added.has(child) || !before) continue;
			const dx = before.left - after.left;
			const dy = before.top - after.top;
			if (Math.abs(dx) >= JUMP || Math.abs(dy) >= JUMP) moves.push({ child, dx, dy });
		}
		// Asked only once something would move: it goes through every child's animations.
		if ((moves.length || entering.length) && [...container.children].some(easingHeight)) return;
		for (const child of entering)
			child.animate(
				[
					{ opacity: 0, transform: 'scale(0.97)' },
					{ opacity: 1, transform: 'none' }
				],
				{ duration: DURATION, easing: EASING }
			);
		for (const { child, dx, dy } of moves)
			child.animate([{ transform: `translate(${dx}px, ${dy}px)` }, { transform: 'none' }], {
				duration: DURATION,
				easing: EASING,
				composite: 'add'
			});
		for (const child of rects.keys()) if (child.parentElement !== container) rects.delete(child);
	}

	/** A copy of a child that has just left, fading out where it stood. */
	function ghost(node: HTMLElement, rect: Rect) {
		if (reduced()) return;
		if (getComputedStyle(container).position === 'static') container.style.position = 'relative';
		const copy = node.cloneNode(true) as HTMLElement;
		copy.dataset[GHOST] = '';
		// A copy must not answer for the real thing: no ids, no test ids, nothing to find it by.
		for (const el of [copy, ...copy.querySelectorAll('*')]) {
			el.removeAttribute('id');
			el.removeAttribute('data-testid');
		}
		copy.setAttribute('aria-hidden', 'true');
		copy.inert = true;
		Object.assign(copy.style, {
			position: 'absolute',
			top: `${rect.top}px`,
			left: `${rect.left}px`,
			width: `${rect.width}px`,
			height: `${rect.height}px`,
			margin: '0',
			pointerEvents: 'none'
		});
		container.append(copy);
		copy
			.animate(
				[
					{ opacity: 1, transform: 'none' },
					{ opacity: 0, transform: 'scale(0.97)' }
				],
				{ duration: DURATION * 0.75, easing: 'ease-in', fill: 'forwards' }
			)
			.finished.finally(() => copy.remove());
	}

	const mutations = new MutationObserver((records) => {
		const added = new Set<HTMLElement>();
		for (const record of records) {
			for (const node of record.addedNodes) if (node instanceof HTMLElement) added.add(node);
			for (const node of record.removedNodes) {
				if (!(node instanceof HTMLElement) || node.dataset[GHOST] !== undefined) continue;
				const rect = rects.get(node);
				if (rect) ghost(node, rect);
			}
		}
		if ([...added].every((n) => n.dataset[GHOST] !== undefined) && added.size > 0) return;
		settle(added);
		observeChildren();
	});
	mutations.observe(container, { childList: true });

	// Size changes inside a child move its siblings without any child being added or removed.
	const sizes = new ResizeObserver(() => settle());
	function observeChildren() {
		sizes.disconnect();
		sizes.observe(container);
		for (const child of container.children) if (!skip(child)) sizes.observe(child);
	}
	observeChildren();

	return () => {
		mutations.disconnect();
		sizes.disconnect();
	};
}
