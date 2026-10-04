<script lang="ts">
	/** The header's one control on the right: the project on GitHub, in a new tab. */
	const REPO = 'https://github.com/antonkesy/YASS';
</script>

<a
	href={REPO}
	target="_blank"
	rel="noreferrer noopener external"
	aria-label="YASS on GitHub"
	title="YASS on GitHub"
	class="flex h-11 w-11 items-center justify-center rounded-[12px] bg-surface text-ink shadow-chip transition-colors hover:bg-accent-wash"
	data-testid="repo-link"
>
	<!--
		Inlined rather than added to $lib/icons: that set is stroke-only (fill="none"),
		and a brand mark is a filled path.
	-->
	<svg width="18" height="18" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
		<path
			d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-2.91-.88-2.91-2.81 0-.82.29-1.49.77-2.01-.1-.21-.34-.98.07-2.04 0 0 .63-.2 2.06.77a6.9 6.9 0 0 1 1.87-.25c.63 0 1.27.08 1.87.25 1.43-.97 2.06-.77 2.06-.77.41 1.06.17 1.83.07 2.04.48.52.77 1.19.77 2.01 0 1.94-1.14 2.61-2.92 2.81.34.29.63.86.63 1.74 0 1.02-.01 1.85-.01 2.1 0 .21.15.46.55.38A7.99 7.99 0 0 0 16 8c0-4.42-3.58-8-8-8Z"
		/>
	</svg>
</a>
