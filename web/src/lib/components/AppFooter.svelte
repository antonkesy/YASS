<script lang="ts">
	/**
	 * One row of outbound links, on every route. Kept to --app-footer-h so the home
	 * screen can subtract a known height and still fit the viewport.
	 */
	const REPO = 'https://github.com/antonkesy/YAWSS';

	// `narrow` marks the one link worth keeping when there is no room for the rest.
	const links = [
		{ href: `${REPO}/issues/new`, label: 'Report an issue', narrow: true },
		{ href: `${REPO}#configuration`, label: 'Configuration', narrow: false },
		{ href: `${REPO}#scanner-setup`, label: 'Scanner setup', narrow: false }
	];
</script>

<footer
	class="flex h-[var(--app-footer-h)] flex-none items-center gap-4 px-4 text-[12px] text-ink-faint sm:gap-5 sm:px-7"
>
	<a
		href={REPO}
		target="_blank"
		rel="noreferrer noopener external"
		class="flex flex-none items-center gap-1.5 font-bold text-ink-muted transition-colors hover:text-accent"
	>
		<!--
			Inlined rather than added to $lib/icons: that set is stroke-only (fill="none"),
			and a brand mark is a filled path.
		-->
		<svg width="14" height="14" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
			<path
				d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-2.91-.88-2.91-2.81 0-.82.29-1.49.77-2.01-.1-.21-.34-.98.07-2.04 0 0 .63-.2 2.06.77a6.9 6.9 0 0 1 1.87-.25c.63 0 1.27.08 1.87.25 1.43-.97 2.06-.77 2.06-.77.41 1.06.17 1.83.07 2.04.48.52.77 1.19.77 2.01 0 1.94-1.14 2.61-2.92 2.81.34.29.63.86.63 1.74 0 1.02-.01 1.85-.01 2.1 0 .21.15.46.55.38A7.99 7.99 0 0 0 16 8c0-4.42-3.58-8-8-8Z"
			/>
		</svg>
		GitHub
	</a>

	<span class="flex-1"></span>

	{#each links as link (link.href)}
		<a
			href={link.href}
			target="_blank"
			rel="noreferrer noopener external"
			class="flex-none font-semibold transition-colors hover:text-accent {link.narrow
				? ''
				: 'hidden sm:block'}"
		>
			{link.label}
		</a>
	{/each}
</footer>
