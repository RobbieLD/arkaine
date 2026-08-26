export interface IconDefinition {
    paths: readonly string[]
    filled?: boolean
}

/*
 * All icons are authored on a 24x24 grid and are horizontally symmetric about
 * x=12 (or, for the play triangle, balanced on its centroid rather than its
 * bounding box) so they land dead centre inside a square icon button.
 */
const definitions = {
    play: {
        filled: true,
        paths: ['M9 5.4v13.2l10.2-6.6z']
    },
    pause: {
        filled: true,
        paths: ['M8 4.75h3.25v14.5H8z', 'M12.75 4.75H16v14.5h-3.25z']
    },
    rewind: {
        filled: true,
        paths: ['M11.5 7.5v9L5.5 12z', 'M18.5 7.5v9L12.5 12z']
    },
    forward: {
        filled: true,
        paths: ['M5.5 7.5v9l6-4.5z', 'M12.5 7.5v9l6-4.5z']
    },
    heart: {
        paths: ['M12 21C12 21 3 15.4 3 9.5C3 6.5 5.4 4 8.4 4C10.1 4 11.4 4.8 12 5.9C12.6 4.8 13.9 4 15.6 4C18.6 4 21 6.5 21 9.5C21 15.4 12 21 12 21Z']
    },
    heartFilled: {
        filled: true,
        paths: ['M12 21C12 21 3 15.4 3 9.5C3 6.5 5.4 4 8.4 4C10.1 4 11.4 4.8 12 5.9C12.6 4.8 13.9 4 15.6 4C18.6 4 21 6.5 21 9.5C21 15.4 12 21 12 21Z']
    },
    plus: {
        paths: ['M12 5.5v13', 'M5.5 12h13']
    },
    minus: {
        paths: ['M5.5 12h13']
    },
    close: {
        paths: ['M6.5 6.5l11 11', 'M17.5 6.5l-11 11']
    },
    menu: {
        paths: ['M4 7h16', 'M4 12h16', 'M4 17h16']
    },
    chevronRight: {
        paths: ['M9 5.5 15.5 12 9 18.5']
    },
    folder: {
        paths: ['M3 7a2 2 0 0 1 2-2h4.2l2 2.4H19a2 2 0 0 1 2 2v8.6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z']
    },
    file: {
        paths: [
            'M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z',
            'M14 3v5h5'
        ]
    },
    image: {
        paths: [
            'M20 4.5H4A1.5 1.5 0 0 0 2.5 6v12A1.5 1.5 0 0 0 4 19.5h16a1.5 1.5 0 0 0 1.5-1.5V6A1.5 1.5 0 0 0 20 4.5z',
            'M8.5 10a1.5 1.5 0 1 1-3 0 1.5 1.5 0 0 1 3 0',
            'M21.5 15.5 16 10l-9 9.5'
        ]
    },
    music: {
        paths: [
            'M9 18V6l10-2v12',
            'M9 18a2.5 2.5 0 1 1-5 0 2.5 2.5 0 0 1 5 0',
            'M19 16a2.5 2.5 0 1 1-5 0 2.5 2.5 0 0 1 5 0'
        ]
    },
    video: {
        paths: [
            'M15 6.5H4.5a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2H15a2 2 0 0 0 2-2v-7a2 2 0 0 0-2-2z',
            'M21.5 8.5 17 12l4.5 3.5z'
        ]
    },
    external: {
        paths: [
            'M14 4.5h5.5V10',
            'M19.5 4.5 11 13',
            'M18 14.5V18a1.5 1.5 0 0 1-1.5 1.5H6A1.5 1.5 0 0 1 4.5 18V7.5A1.5 1.5 0 0 1 6 6h3.5'
        ]
    },
    logout: {
        paths: ['M9.5 19.5H6A1.5 1.5 0 0 1 4.5 18V6A1.5 1.5 0 0 1 6 4.5h3.5', 'M15 16l4-4-4-4', 'M19 12H9.5']
    },
    trash: {
        paths: [
            'M4.5 6.5h15',
            'M9 6.5V5a1.5 1.5 0 0 1 1.5-1.5h3A1.5 1.5 0 0 1 15 5v1.5',
            'M6.5 6.5 7.4 19a1.5 1.5 0 0 0 1.5 1.4h6.2a1.5 1.5 0 0 0 1.5-1.4l.9-12.5'
        ]
    },
    check: {
        paths: ['M5 12.5 9.5 17 19 7.5']
    },
    refresh: {
        paths: [
            'M19.5 12a7.5 7.5 0 1 1-2.2-5.3',
            'M19.5 4v4.5H15'
        ]
    },
    key: {
        paths: [
            'M14.5 3.5a6 6 0 1 0-4.9 9.45L8 14.5H5.5v2.5H3v3.5h5.5v-2.5H11v-2.5l1.55-1.6a6 6 0 0 0 1.95-10.4z',
            'M16 8.25a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0'
        ]
    }
} as const

export type IconName = keyof typeof definitions

export const icons: Record<IconName, IconDefinition> = definitions
