// =============================================================================
//  (tabs)/_layout.tsx  —  Alt Tab Navigasyon Çubuğu
// =============================================================================

import { Tabs } from 'expo-router';
import { View, StyleSheet, Platform } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Colors } from '@/constants/Colors';
import { BlurView } from 'expo-blur';

type IoniconsName = React.ComponentProps<typeof Ionicons>['name'];

interface TabInfo {
  name: string;
  title: string;
  icon: IoniconsName;
  iconFocused: IoniconsName;
}

const TABS: TabInfo[] = [
  { name: 'index',    title: 'Bağlantı',  icon: 'wifi-outline',    iconFocused: 'wifi'         },
  { name: 'control',  title: 'Kontrol',   icon: 'color-palette-outline', iconFocused: 'color-palette' },
  { name: 'modes',    title: 'Modlar',    icon: 'flash-outline',   iconFocused: 'flash'         },
  { name: 'settings', title: 'Ayarlar',   icon: 'settings-outline', iconFocused: 'settings'     },
];

export default function TabLayout() {
  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarStyle: styles.tabBar,
        tabBarBackground: () => (
          Platform.OS === 'ios'
            ? <BlurView tint="dark" intensity={80} style={StyleSheet.absoluteFill} />
            : <View style={[StyleSheet.absoluteFill, styles.tabBarBg]} />
        ),
        tabBarActiveTintColor:   Colors.accent,
        tabBarInactiveTintColor: Colors.tabInactive,
        tabBarLabelStyle: styles.tabLabel,
        tabBarItemStyle:  styles.tabItem,
      }}
    >
      {TABS.map(tab => (
        <Tabs.Screen
          key={tab.name}
          name={tab.name}
          options={{
            title: tab.title,
            tabBarIcon: ({ focused, color, size }) => (
              <Ionicons
                name={focused ? tab.iconFocused : tab.icon}
                size={size}
                color={color}
              />
            ),
          }}
        />
      ))}
    </Tabs>
  );
}

const styles = StyleSheet.create({
  tabBar: {
    borderTopWidth: 1,
    borderTopColor: Colors.border,
    height: Platform.OS === 'ios' ? 88 : 64,
    paddingBottom: Platform.OS === 'ios' ? 24 : 8,
    backgroundColor: 'transparent',
    position: 'absolute',
    elevation: 0,
  },
  tabBarBg: {
    backgroundColor: Colors.bgPanel,
    borderTopWidth: 1,
    borderTopColor: Colors.border,
  },
  tabLabel: {
    fontSize: 10,
    fontWeight: '600',
    marginBottom: 2,
  },
  tabItem: {
    paddingTop: 8,
  },
});
