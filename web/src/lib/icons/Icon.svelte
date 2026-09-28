<script lang="ts">
	import { DEFAULT_SIZE, DEFAULT_STROKE, icons, type IconName, type IconSpec } from './paths';

	let {
		name,
		size,
		stroke,
		class: klass = ''
	}: {
		name: IconName;
		/** Overrides the size the mockup draws this glyph at. */
		size?: number;
		stroke?: number;
		class?: string;
	} = $props();

	// Widened to IconSpec: the `as const` map narrows each entry to its own literal
	// shape, so glyphs without a size override would otherwise have no such property.
	const spec: IconSpec = $derived(icons[name]);
	const px = $derived(size ?? spec.size ?? DEFAULT_SIZE);
	const width = $derived(stroke ?? spec.stroke ?? DEFAULT_STROKE);
</script>

<svg
	width={px}
	height={px}
	viewBox="0 0 24 24"
	fill="none"
	stroke="currentColor"
	stroke-width={width}
	stroke-linecap="round"
	stroke-linejoin="round"
	class="block flex-none {klass}"
	aria-hidden="true"
>
	{#each spec.shapes as shape, i (i)}
		{#if shape.kind === 'path'}
			<path d={shape.d} />
		{:else if shape.kind === 'rect'}
			<rect x={shape.x} y={shape.y} width={shape.w} height={shape.h} rx={shape.rx} />
		{:else}
			<circle cx={shape.cx} cy={shape.cy} r={shape.r} />
		{/if}
	{/each}
</svg>
