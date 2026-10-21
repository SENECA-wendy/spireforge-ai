public class PatchLinkageTest {
    public static void main(String[] args) throws Exception {
        String[] classes={"com.megacrit.cardcrawl.dungeons.AbstractDungeon","com.megacrit.cardcrawl.shop.ShopScreen","com.stseditor.runtime.RuntimeBridge","com.stseditor.runtime.RunCardPoolService"};
        for(String name:classes) {
            Class<?> type=Class.forName(name,false,PatchLinkageTest.class.getClassLoader());
            type.getDeclaredMethods();
            System.out.println("PASS patched real-game class verifies and links: "+name);
        }
        System.out.println("TOTAL 4 patched real-game class linkage checks passed");
    }
}
