import type { Area } from '../types';

/** The edit dialogs, the keyboard shortcuts, the toast and the client's own error text. */
export const dialogs = {
	cancel: { en: 'Cancel', de: 'Abbrechen', fr: 'Annuler', es: 'Cancelar' },
	apply: { en: 'Apply', de: 'Anwenden', fr: 'Appliquer', es: 'Aplicar' },
	reset: { en: 'Reset', de: 'Zurücksetzen', fr: 'Réinitialiser', es: 'Restablecer' },

	// Crop
	cropTitle: {
		en: 'Crop page',
		de: 'Seite zuschneiden',
		fr: 'Rogner la page',
		es: 'Recortar página'
	},
	cancelCrop: {
		en: 'Cancel crop',
		de: 'Zuschneiden abbrechen',
		fr: 'Annuler le rognage',
		es: 'Cancelar recorte'
	},
	edgeTop: { en: 'Top', de: 'Oben', fr: 'Haut', es: 'Arriba' },
	edgeRight: { en: 'Right', de: 'Rechts', fr: 'Droite', es: 'Derecha' },
	edgeBottom: { en: 'Bottom', de: 'Unten', fr: 'Bas', es: 'Abajo' },
	edgeLeft: { en: 'Left', de: 'Links', fr: 'Gauche', es: 'Izquierda' },
	cropImpossible: {
		en: 'A crop has to leave at least 5% of the page on each axis.',
		de: 'Ein Zuschnitt muss auf jeder Achse mindestens 5 % der Seite übrig lassen.',
		fr: 'Un rognage doit garder au moins 5 % de la page sur chaque axe.',
		es: 'Un recorte debe dejar al menos un 5 % de la página en cada eje.'
	},
	removeCrop: {
		en: 'Remove crop',
		de: 'Zuschnitt entfernen',
		fr: 'Retirer le rognage',
		es: 'Quitar recorte'
	},
	applyCrop: {
		en: 'Apply crop',
		de: 'Zuschneiden',
		fr: 'Rogner',
		es: 'Recortar'
	},

	// Adjust
	adjustTitle: {
		en: { one: 'Adjust page', other: 'Adjust {n} pages' },
		de: { one: 'Seite anpassen', other: '{n} Seiten anpassen' },
		fr: { one: 'Ajuster la page', other: 'Ajuster {n} pages' },
		es: { one: 'Ajustar página', other: 'Ajustar {n} páginas' }
	},
	cancelAdjust: {
		en: 'Cancel adjust',
		de: 'Anpassen abbrechen',
		fr: "Annuler l'ajustement",
		es: 'Cancelar ajuste'
	},
	brightness: { en: 'Brightness', de: 'Helligkeit', fr: 'Luminosité', es: 'Brillo' },
	contrast: { en: 'Contrast', de: 'Kontrast', fr: 'Contraste', es: 'Contraste' },
	sharpen: { en: 'Sharpen', de: 'Schärfen', fr: 'Netteté', es: 'Nitidez' },
	adjustMany: {
		en: 'Starts from the page in view. Apply sets all three on every selected page.',
		de: 'Startet bei der angezeigten Seite. Anwenden setzt alle drei Werte auf jeder ausgewählten Seite.',
		fr: 'Part de la page affichée. Appliquer règle les trois valeurs sur chaque page sélectionnée.',
		es: 'Parte de la página visible. Aplicar fija los tres valores en cada página seleccionada.'
	},
	resetAll: {
		en: 'Reset all',
		de: 'Alles zurücksetzen',
		fr: 'Tout réinitialiser',
		es: 'Restablecer todo'
	},

	// Straighten
	straightenTitle: {
		en: 'Straighten page',
		de: 'Seite begradigen',
		fr: 'Redresser la page',
		es: 'Enderezar página'
	},
	cancelStraighten: {
		en: 'Cancel straighten',
		de: 'Begradigen abbrechen',
		fr: 'Annuler le redressement',
		es: 'Cancelar enderezado'
	},
	angle: { en: 'Angle', de: 'Winkel', fr: 'Angle', es: 'Ángulo' },
	turnAnticlockwise: {
		en: 'Turn a tenth of a degree anticlockwise',
		de: 'Um ein Zehntelgrad gegen den Uhrzeigersinn drehen',
		fr: "Tourner d'un dixième de degré dans le sens antihoraire",
		es: 'Girar una décima de grado en sentido antihorario'
	},
	turnClockwise: {
		en: 'Turn a tenth of a degree clockwise',
		de: 'Um ein Zehntelgrad im Uhrzeigersinn drehen',
		fr: "Tourner d'un dixième de degré dans le sens horaire",
		es: 'Girar una décima de grado en sentido horario'
	},
	lineUp: {
		en: 'Line the text up with the grid',
		de: 'Richte den Text am Raster aus',
		fr: 'Aligne le texte sur la grille',
		es: 'Alinea el texto con la cuadrícula'
	},

	// Merge
	mergeTitle: {
		en: 'Add pages from a past scan',
		de: 'Seiten aus einem früheren Scan hinzufügen',
		fr: "Ajouter des pages d'un ancien scan",
		es: 'Añadir páginas de un escaneo anterior'
	},
	mergeHint: {
		en: 'Its pages are copied to the end of this scan as their own document. The other scan is left as it is.',
		de: 'Seine Seiten werden als eigenes Dokument ans Ende dieses Scans kopiert. Der andere Scan bleibt unverändert.',
		fr: "Ses pages sont copiées à la fin de ce scan comme un document à part. L'autre scan reste tel quel.",
		es: 'Sus páginas se copian al final de este escaneo como un documento aparte. El otro escaneo no cambia.'
	},
	mergeNone: {
		en: 'No other scan has pages to add.',
		de: 'Kein anderer Scan hat Seiten zum Hinzufügen.',
		fr: "Aucun autre scan n'a de pages à ajouter.",
		es: 'Ningún otro escaneo tiene páginas para añadir.'
	},
	pages: {
		en: { one: '{n} page', other: '{n} pages' },
		de: { one: '{n} Seite', other: '{n} Seiten' },
		fr: { one: '{n} page', other: '{n} pages' },
		es: { one: '{n} página', other: '{n} páginas' }
	},

	// Keyboard shortcuts
	shortcutsTitle: {
		en: 'Keyboard shortcuts',
		de: 'Tastenkürzel',
		fr: 'Raccourcis clavier',
		es: 'Atajos de teclado'
	},
	closeShortcuts: {
		en: 'Close keyboard shortcuts',
		de: 'Tastenkürzel schließen',
		fr: 'Fermer les raccourcis clavier',
		es: 'Cerrar atajos de teclado'
	},
	groupEverywhere: { en: 'Everywhere', de: 'Überall', fr: 'Partout', es: 'En todas partes' },
	groupReview: { en: 'Review', de: 'Prüfen', fr: 'Relecture', es: 'Revisión' },
	groupFullScreen: {
		en: 'Full-screen page',
		de: 'Seite im Vollbild',
		fr: 'Page en plein écran',
		es: 'Página a pantalla completa'
	},
	allPages: {
		en: 'All pages',
		de: 'Alle Seiten',
		fr: 'Toutes les pages',
		es: 'Todas las páginas'
	},
	keyEsc: { en: 'Esc', de: 'Esc', fr: 'Échap', es: 'Esc' },
	keyEnter: { en: 'Enter', de: 'Eingabe', fr: 'Entrée', es: 'Intro' },
	keyShift: { en: 'Shift', de: 'Umschalt', fr: 'Maj', es: 'Mayús' },
	keyDel: { en: 'Del', de: 'Entf', fr: 'Suppr', es: 'Supr' },
	keyCtrl: { en: 'Ctrl', de: 'Strg', fr: 'Ctrl', es: 'Ctrl' },
	keyAlt: { en: 'Alt', de: 'Alt', fr: 'Alt', es: 'Alt' },
	keyClose: {
		en: 'Close what is open, or go back',
		de: 'Offenes schließen oder zurückgehen',
		fr: 'Fermer ce qui est ouvert, ou revenir',
		es: 'Cerrar lo abierto o volver'
	},
	keyHelp: {
		en: 'Show or hide these shortcuts',
		de: 'Diese Tastenkürzel ein- oder ausblenden',
		fr: 'Afficher ou masquer ces raccourcis',
		es: 'Mostrar u ocultar estos atajos'
	},
	keyPrevNext: {
		en: 'Previous / next page',
		de: 'Vorherige / nächste Seite',
		fr: 'Page précédente / suivante',
		es: 'Página anterior / siguiente'
	},
	keyOpen: {
		en: 'Open the page full screen',
		de: 'Seite im Vollbild öffnen',
		fr: 'Ouvrir la page en plein écran',
		es: 'Abrir la página a pantalla completa'
	},
	keyRotateRight: {
		en: 'Rotate right',
		de: 'Nach rechts drehen',
		fr: 'Pivoter à droite',
		es: 'Girar a la derecha'
	},
	keyRotateLeft: {
		en: 'Rotate left',
		de: 'Nach links drehen',
		fr: 'Pivoter à gauche',
		es: 'Girar a la izquierda'
	},
	keyDelete: {
		en: 'Delete the page (undo from the toast)',
		de: 'Seite löschen (rückgängig über die Meldung)',
		fr: 'Supprimer la page (annulable depuis la notification)',
		es: 'Eliminar la página (deshacer desde el aviso)'
	},
	keySave: {
		en: 'Save as…',
		de: 'Speichern unter…',
		fr: 'Enregistrer sous…',
		es: 'Guardar como…'
	},
	keyMove: {
		en: 'Move the focused page in the strip',
		de: 'Fokussierte Seite in der Leiste verschieben',
		fr: 'Déplacer la page active dans la bande',
		es: 'Mover la página activa en la tira'
	},
	keyZoom: {
		en: 'Zoom to full width and back',
		de: 'Auf volle Breite zoomen und zurück',
		fr: 'Zoomer en pleine largeur et revenir',
		es: 'Ampliar a todo el ancho y volver'
	},
	keySize: {
		en: 'Bigger / smaller pages',
		de: 'Größere / kleinere Seiten',
		fr: 'Pages plus grandes / plus petites',
		es: 'Páginas más grandes / más pequeñas'
	},

	// Toast and errors
	undo: { en: 'Undo', de: 'Rückgängig', fr: 'Annuler', es: 'Deshacer' },
	serverError: {
		en: 'Something went wrong on the server.',
		de: 'Auf dem Server ist etwas schiefgelaufen.',
		fr: "Une erreur s'est produite sur le serveur.",
		es: 'Algo salió mal en el servidor.'
	}
} satisfies Area;
