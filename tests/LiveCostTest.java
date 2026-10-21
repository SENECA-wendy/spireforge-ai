import com.stseditor.runtime.LiveCostPolicy;
public class LiveCostTest {
    public static void main(String[] args) {
        int[][] cases={{2,2,0,0},{2,0,0,0},{2,0,1,0},{2,1,3,2},{1,2,2,3},{1,0,-1,-1},{1,0,-2,-2},{-1,-1,1,1},{-2,-2,0,0}};
        for(int[] c:cases) if(LiveCostPolicy.afterBaseCostChange(c[0],c[1],c[2])!=c[3]) throw new AssertionError(java.util.Arrays.toString(c));
        System.out.println("TOTAL 9 live-cost checks passed (discount floor and X/unplayable transitions)");
    }
}
